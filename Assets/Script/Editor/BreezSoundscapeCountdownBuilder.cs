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

    static readonly Color Ink = new Color32(51, 51, 51, 255);
    static readonly Color Grey = new Color32(112, 112, 112, 255);
    static readonly Color Track = new Color32(0xEE, 0xEF, 0xE8, 255);
    static readonly Color Sage = new Color32(0xA9, 0xC7, 0x7E, 255);

    static TMP_FontAsset regular, bold;
    static Sprite pill, circle, ring;

    [MenuItem("Breez/Restyle Soundscape Countdown")]
    public static void Restyle()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        regular = BreezThaiFontInstaller.EnsureFonts();
        bold = BreezThaiFontInstaller.BoldFont;
        if (bold == null) bold = regular;
        pill = BreezStatusElementsBuilder.MakeSprite("ui_pill", BreezSpriteArt.RoundedRect(128, 63), 128, 63);
        circle = BreezStatusElementsBuilder.MakeSprite("ui_circle", BreezSpriteArt.Circle(256), 256, 0);
        ring = BreezStatusElementsBuilder.MakeSprite("ui_ring", BreezSpriteArt.Ring(512, 0.09f), 512, 0);

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

            // ป้าย Start อยู่บนสุด (ถ้ามี) แล้วตามด้วยนับถอยหลัง
            sp.breathGuide.transform.SetAsLastSibling();
            sp.countdownUI.transform.SetAsLastSibling();

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
    static SoundscapeCountdownUI BuildCountdown(Transform C)
    {
        var old = C.Find("CountdownOverlay");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = UI("CountdownOverlay", C); Stretch(root);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        var dim = Img(root, "Dim", null, new Color(0.12f, 0.16f, 0.14f, 0.22f)); Stretch(dim.rectTransform);

        var bubble = UI("Bubble", root); Place(bubble, 0, 20, 420, 420);
        var shadow = Img(bubble, "Shadow", circle, new Color(0, 0, 0, 0.10f)); Place(shadow.rectTransform, 0, -10, 400, 400);
        var face = Img(bubble, "Face", circle, Color.white); Place(face.rectTransform, 0, 0, 400, 400);
        var track = Img(bubble, "RingTrack", ring, Track); Place(track.rectTransform, 0, 0, 360, 360);
        var r = Img(bubble, "Ring", ring, Sage); Place(r.rectTransform, 0, 0, 360, 360);
        r.type = Image.Type.Filled;
        r.fillMethod = Image.FillMethod.Radial360;
        r.fillOrigin = (int)Image.Origin360.Top;
        r.fillClockwise = false;
        r.fillAmount = 1f;
        var num = Text(bubble, "Number", "3", bold, 170, Ink, 0, 4, 360, 220);
        var cap = Text(root, "Caption", "เตรียมตัว หายใจตามจังหวะ", regular, 36, Color.white, 0, -260, 1000, 60);
        var capShadow = cap.gameObject.AddComponent<Shadow>();
        capShadow.effectColor = new Color(0, 0, 0, 0.25f);
        capShadow.effectDistance = new Vector2(0, -2);

        var ui = root.gameObject.AddComponent<SoundscapeCountdownUI>();
        ui.group = group;
        ui.bubble = bubble;
        ui.ring = r;
        ui.number = num;
        ui.caption = cap;
        group.alpha = 0f;
        root.gameObject.SetActive(false);
        return ui;
    }

    static BreathGuideUI BuildGuide(Transform C)
    {
        var old = C.Find("BreathGuide");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var card = Img(C, "BreathGuide", pill, Color.white);
        card.type = Image.Type.Sliced;
        Place(card.rectTransform, 0, 382, 600, 150);
        var sh = card.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0, 0, 0, 0.10f);
        sh.effectDistance = new Vector2(0, -5);
        var group = card.gameObject.AddComponent<CanvasGroup>();
        var T = card.transform;

        // วงกลมหายใจ
        var halo = Img(T, "CircleHalo", circle, new Color32(0xF3, 0xF5, 0xEC, 255)); Place(halo.rectTransform, -205, 0, 112, 112);
        var dot = Img(T, "Circle", circle, new Color32(0xB9, 0xDA, 0xEC, 255)); Place(dot.rectTransform, -205, 0, 112, 112);

        var phase = Text(T, "Phase", "หายใจเข้า", bold, 46, Ink, -10, 18, 260, 64);
        phase.alignment = TextAlignmentOptions.Left;
        Place(phase.rectTransform, -130, 18, 260, 64, 0f);

        var trackBar = Img(T, "Track", pill, Track); trackBar.type = Image.Type.Sliced;
        Place(trackBar.rectTransform, -130, -34, 240, 14, 0f);
        var fill = Img(trackBar.transform, "Fill", pill, new Color32(0xB9, 0xDA, 0xEC, 255));
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0;
        Stretch(fill.rectTransform);

        var secs = Text(T, "Seconds", "5", bold, 84, Ink, 205, 12, 140, 100);
        var unit = Text(T, "Unit", "วินาที", regular, 24, Grey, 205, -46, 140, 34);

        var ui = card.gameObject.AddComponent<BreathGuideUI>();
        ui.group = group;
        ui.phaseText = phase;
        ui.secondsText = secs;
        ui.unitText = unit;
        ui.progressFill = fill;
        ui.breathCircle = dot.rectTransform;
        ui.breathCircleImage = dot;
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
