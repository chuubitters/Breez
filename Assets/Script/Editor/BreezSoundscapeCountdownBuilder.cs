using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// Breez > Restyle Soundscape Countdown
// ใช้กับ Rainy Forest + Windy Mountain (กดซ้ำได้)
//   • นับถอยหลัง 3 · 2 · 1 · เริ่ม! : วงกลมขาวกลางจอ วงแหวนลดลงทีละวินาที ตัวเลขเด้ง
//   • ป้ายนำการหายใจ: วงกลมขยาย/หด + "หายใจเข้า / หายใจออก" + วินาทีที่เหลือ + แถบความคืบหน้า
//   ข้อความ/Slider แบบเดิมจะถูกซ่อน (ไม่ลบ)
// =====================================================================
public static class BreezSoundscapeCountdownBuilder
{
    static readonly string[] Scenes = { "Assets/Scenes/Rainy Forest.unity", "Assets/Scenes/Windy Mountain.unity" };

    static readonly Color Ink = new Color32(0x2E, 0x3B, 0x35, 255);
    static readonly Color RingTrack = new Color(1f, 1f, 1f, 0.55f);
    static readonly Color Grey = new Color32(112, 112, 112, 255);
    static readonly Color Track = new Color32(0xEE, 0xEF, 0xE8, 255);
    static readonly Color Sage = new Color32(0xA9, 0xC7, 0x7E, 255);

    static TMP_FontAsset regular, bold;
    static Sprite pill, circle, ring, gradientRing;

    [MenuItem("Breez/Restyle Soundscape Countdown")]
    public static void Restyle()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        regular = BreezThaiFontInstaller.EnsureFonts();
        bold = BreezThaiFontInstaller.BoldFont;
        if (bold == null) bold = regular;
        pill = BreezStatusElementsBuilder.MakeSprite("ui_pill", BreezSpriteArt.RoundedRect(128, 63), 128, 63);
        circle = BreezStatusElementsBuilder.MakeSprite("ui_circle", BreezSpriteArt.Circle(256), 256, 0);
        ring = BreezStatusElementsBuilder.MakeSprite("ui_ring_track", BreezSpriteArt.Ring(512, 0.09f), 512, 0);
        gradientRing = BreezStatusElementsBuilder.MakeSprite("ui_ring_gradient", BreezSpriteArt.GradientRing(512, 0.09f,
            BreezSpriteArt.Rgba.Hex(0x7FB8EE), BreezSpriteArt.Rgba.Hex(0x7CD8B2)), 512, 0);

        string previous = EditorSceneManager.GetActiveScene().path;
        var log = new System.Text.StringBuilder();
        foreach (var path in Scenes)
        {
            if (!File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var sp = Object.FindObjectOfType<SPGameManager>();
            if (sp == null) { log.AppendLine("✗ " + Path.GetFileNameWithoutExtension(path) + ": ไม่พบ SPGameManager"); continue; }

            Canvas canvas = null;
            if (sp.countdownText != null) canvas = sp.countdownText.canvas != null ? sp.countdownText.canvas.rootCanvas : null;
            if (canvas == null) canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null) { log.AppendLine("✗ " + Path.GetFileNameWithoutExtension(path) + ": ไม่พบ Canvas"); continue; }
            var C = canvas.transform;

            Undo.RecordObject(sp, "Restyle countdown");
            // ของเดิม: ซ่อนไว้
            if (sp.countdownText != null) sp.countdownText.gameObject.SetActive(false);
            if (sp.breathingText != null) sp.breathingText.gameObject.SetActive(false);
            if (sp.breathingBar != null) sp.breathingBar.gameObject.SetActive(false);

            sp.breathGuide = BuildGuide(C);
            sp.countdownUI = BuildCountdown(C);

            // ป้ายนับถอยหลังอยู่บนสุด แต่ไม่บังปุ่ม Start (start panel จะถูกยกขึ้นบนสุดอีกที)
            sp.breathGuide.transform.SetAsLastSibling();
            sp.countdownUI.transform.SetAsLastSibling();
            if (sp.startPanel != null && sp.startPanel.transform.parent == C) sp.startPanel.transform.SetAsLastSibling();

            BreezThaiFontInstaller.Apply(canvas.gameObject);
            EditorUtility.SetDirty(sp);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("✓ " + Path.GetFileNameWithoutExtension(path));
        }
        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Breez", "ปรับหน้านับถอยหลัง Soundscape แล้ว\n\n" + log + "\nกด Play แล้วกดเริ่มเพื่อดู", "OK");
    }

    // ---------------------------------------------------------------
    // ดีไซน์: ข้อความด้านบน + วงแหวนไล่สี (ฟ้า -> เขียวมิ้นต์) + ตัวเลขใหญ่ตรงกลาง ไม่มีพื้นหลังทึบ
    const float RingSize = 270f;

    static Image Ring(Transform parent)
    {
        var track = Img(parent, "RingTrack", ring, RingTrack); Place(track.rectTransform, 0, 0, RingSize, RingSize);
        var r = Img(parent, "Ring", gradientRing, Color.white); Place(r.rectTransform, 0, 0, RingSize, RingSize);
        r.type = Image.Type.Filled;
        r.fillMethod = Image.FillMethod.Radial360;
        r.fillOrigin = (int)Image.Origin360.Top;
        r.fillClockwise = false;
        r.fillAmount = 1f;
        var glow = r.gameObject.AddComponent<Shadow>();
        glow.effectColor = new Color(1f, 1f, 1f, 0.35f);
        glow.effectDistance = new Vector2(0, -2);
        return r;
    }

    static SoundscapeCountdownUI BuildCountdown(Transform C)
    {
        var old = C.Find("CountdownOverlay");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = UI("CountdownOverlay", C); Stretch(root);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        var hit = Img(root, "Blocker", null, new Color(1f, 1f, 1f, 0.12f)); Stretch(hit.rectTransform);

        var center = UI("Center", root); Place(center, 0, 0, RingSize, RingSize);
        var r = Ring(center);
        var num = Text(center, "Number", "3", regular, 150, Ink, 0, 2, RingSize, 200);
        var cap = Text(root, "Caption", "เตรียมตัว หายใจตามจังหวะ", regular, 54, Ink, 0, 190, 1200, 80);

        var ui = root.gameObject.AddComponent<SoundscapeCountdownUI>();
        ui.group = group;
        ui.bubble = center;
        ui.ring = r;
        ui.number = num;
        ui.caption = cap;
        ui.numberSize = 150;
        ui.goSize = 96;
        group.alpha = 0f;
        root.gameObject.SetActive(false);
        return ui;
    }

    static BreathGuideUI BuildGuide(Transform C)
    {
        var old = C.Find("BreathGuide");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = UI("BreathGuide", C);
        Place(root, 0, 40, 1200, 520);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        var phase = Text(root, "Phase", "สูดลมหายใจเข้า", regular, 54, Ink, 0, 188, 1200, 80);
        var center = UI("Center", root); Place(center, 0, 0, RingSize, RingSize);
        var r = Ring(center);
        var secs = Text(center, "Seconds", "5", regular, 150, Ink, 0, 2, RingSize, 200);

        var ui = root.gameObject.AddComponent<BreathGuideUI>();
        ui.group = group;
        ui.phaseText = phase;
        ui.secondsText = secs;
        ui.progressFill = r;
        ui.ringShowsRemaining = true;
        ui.tintProgress = false;
        group.alpha = 0f;
        return ui;
    }

    // ---------------------------------------------------------------
    static RectTransform UI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image Img(Transform parent, string name, Sprite sprite, Color color)
    {
        var img = UI(name, parent).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
                                float x, float y, float w, float h)
    {
        var rt = UI(name, parent);
        Place(rt, x, y, w, h);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    /// <summary>pivotX = 0 => x คือขอบซ้าย</summary>
    static void Place(RectTransform rt, float x, float y, float w, float h, float pivotX = 0.5f)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(pivotX, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
