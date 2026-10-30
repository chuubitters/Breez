using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// Breez > Restyle Soundscape Countdown
// ใช้กับ Rainy Forest + Windy Mountain (กดซ้ำได้)
//   • นับถอยหลัง 3 · 2 · 1 · เริ่ม! : วงกลมขาวกลางจอ วงแหวนลดลงทีละวินาที ตัวเลขเด้ง
//   • ป้ายนำการหายใจ: วงกลมขยาย/หด + "หายใจเข้า / หายใจออก" + วินาทีที่เหลือ + แถบความคืบหน้า
//   • ปุ่ม "จบการฝึก" => หน้าผลลัพธ์ (เวลาที่ใช้ในโหมดนี้ / รอบหายใจ / เวลาที่เป่า) + Replay / Home
//   • Windy Mountain: จัดตำแหน่งหัวข้อ / ปุ่มกลับ / สถานะอุปกรณ์ / ปุ่มจบ ให้เหมือน Rainy Forest
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

    static readonly Color Black = new Color32(34, 34, 34, 255);
    static readonly Color HeaderPink = new Color32(252, 227, 232, 255);
    static readonly Color Pink = new Color32(242, 189, 201, 255);
    static readonly Color Green = new Color32(212, 229, 157, 255);

    static TMP_FontAsset regular, bold;
    static Sprite pill, circle, ring, gradientRing, card, arrow;

    /// <summary>ตำแหน่ง UI ของ Rainy Forest (ต้นแบบ) — ใช้จัด Windy Mountain ให้เหมือนกัน</summary>
    class Layout
    {
        public Vector2 titlePos, titleSize, backPos, backSize, gadgetPos, gadgetSize, endPos, endSize;
        public float titleFontSize; public Color titleColor; public TMP_FontAsset titleFont; public FontStyles titleStyle;
        public bool hasTitle, hasBack, hasGadget, hasEnd;
    }

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
        card = BreezStatusElementsBuilder.MakeSprite("ui_card", BreezSpriteArt.RoundedRect(128, 40), 128, 40);
        arrow = BreezStatusElementsBuilder.MakeSprite("ui_arrow_left", BreezSpriteArt.ArrowLeft(128), 128, 0);

        string previous = EditorSceneManager.GetActiveScene().path;
        var log = new System.Text.StringBuilder();
        Layout model = null;
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

            // ---------- จัด layout: Rainy Forest = ต้นแบบ, Windy Mountain = ตามต้นแบบ ----------
            var title = FindTitle(C, sp);
            var back = FindButton(C, "back");
            var end = FindButton(C, "end");
            var gadget = C.Find("GadgetStatus") as RectTransform;
            if (model == null) model = Capture(title, back, gadget, end);
            else ApplyLayout(model, title, back, gadget, end);
            if (back != null) WireBack(back);

            // ---------- จบการฝึก + หน้าผลลัพธ์ ----------
            if (end != null) sp.endButton = end;
            sp.resultUI = BuildResult();
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
        EditorUtility.DisplayDialog("Breez", "ปรับหน้า Soundscape แล้ว\n\n" + log +
            "\n• นับถอยหลัง + ป้ายนำการหายใจ\n• กด \"จบการฝึก\" => หน้าผลลัพธ์ (เวลาที่ใช้)\n• Windy Mountain จัดตำแหน่งตาม Rainy Forest\n\nกด Play เพื่อทดสอบ", "OK");
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
        var hit = Img(root, "Blocker", null, new Color(1f, 1f, 1f, 0.94f)); Stretch(hit.rectTransform);   // พื้นหลังขาวตอนนับถอยหลัง
        hit.raycastTarget = true;

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
    // Layout
    static TMP_Text FindTitle(Transform C, SPGameManager sp)
    {
        foreach (Transform ch in C)
        {
            if (!ch.gameObject.activeSelf) continue;
            var t = ch.GetComponent<TMP_Text>();
            if (t == null || t == sp.countdownText || t == sp.breathingText) continue;
            return t;
        }
        return null;
    }

    /// <summary>ปุ่มลูกของ Canvas ที่ชื่อ/รูป มีคำว่า key (back / end)</summary>
    static Button FindButton(Transform C, string key)
    {
        foreach (Transform ch in C)
        {
            var b = ch.GetComponent<Button>();
            if (b == null || !ch.gameObject.activeSelf) continue;
            var img = ch.GetComponent<Image>();
            string n = ch.name.ToLower() + " " + (img != null && img.sprite != null ? img.sprite.name.ToLower() : "");
            if (n.Contains(key)) return b;
        }
        return null;
    }

    static Layout Capture(TMP_Text title, Button back, RectTransform gadget, Button end)
    {
        var l = new Layout();
        if (title != null)
        {
            l.hasTitle = true; var rt = title.rectTransform; l.titlePos = rt.anchoredPosition; l.titleSize = rt.sizeDelta;
            l.titleFontSize = title.fontSize; l.titleColor = title.color; l.titleFont = title.font; l.titleStyle = title.fontStyle;
        }
        if (back != null) { l.hasBack = true; var rt = (RectTransform)back.transform; l.backPos = rt.anchoredPosition; l.backSize = rt.sizeDelta; }
        if (gadget != null) { l.hasGadget = true; l.gadgetPos = gadget.anchoredPosition; l.gadgetSize = gadget.sizeDelta; }
        if (end != null) { l.hasEnd = true; var rt = (RectTransform)end.transform; l.endPos = rt.anchoredPosition; l.endSize = rt.sizeDelta; }
        return l;
    }

    static void ApplyLayout(Layout l, TMP_Text title, Button back, RectTransform gadget, Button end)
    {
        if (title != null && l.hasTitle)
        {
            var rt = title.rectTransform; Undo.RecordObject(rt, "Layout"); Undo.RecordObject(title, "Layout");
            rt.anchoredPosition = l.titlePos; rt.sizeDelta = l.titleSize;
            title.fontSize = l.titleFontSize; title.color = l.titleColor; title.fontStyle = l.titleStyle;
            if (l.titleFont != null) title.font = l.titleFont;
        }
        if (back != null && l.hasBack) Move((RectTransform)back.transform, l.backPos, l.backSize);
        if (gadget != null && l.hasGadget) Move(gadget, l.gadgetPos, l.gadgetSize);
        if (end != null && l.hasEnd) Move((RectTransform)end.transform, l.endPos, l.endSize);
    }

    static void Move(RectTransform rt, Vector2 pos, Vector2 size)
    {
        Undo.RecordObject(rt, "Layout");
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    /// <summary>ปุ่มกลับที่ยังไม่ได้ผูก => กลับไปหน้าเลือก Soundscape</summary>
    static void WireBack(Button b)
    {
        if (b.onClick.GetPersistentEventCount() > 0) return;
        string target = null;
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!s.enabled) continue;
            string n = Path.GetFileNameWithoutExtension(s.path);
            string k = new string(System.Array.FindAll(n.ToCharArray(), ch => char.IsLetter(ch))).ToLower();
            if (k == "soundscape") { target = n; break; }
        }
        if (target == null) target = "Mode";
        var loader = b.GetComponent<SceneLoader>();
        if (loader == null) loader = Undo.AddComponent<SceneLoader>(b.gameObject);
        UnityEventTools.AddStringPersistentListener(b.onClick, loader.LoadSceneByName, target);
    }

    // ---------------------------------------------------------------
    // หน้าผลลัพธ์ (การ์ดขาวแบบเดียวกับ Breathe & Focus) อยู่ใน BreezTopCanvas => บนสุดเสมอ
    static SoundscapeResultUI BuildResult()
    {
        var top = BreezBreathPlayLayers.TopCanvas();
        var old = top.Find("SoundscapeResult");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var root = UI("SoundscapeResult", top); Stretch(root);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        var dim = Img(root, "Dim", null, new Color(1f, 0.97f, 0.97f, 0.45f)); Stretch(dim.rectTransform); dim.raycastTarget = true;

        var c = Img(root, "Card", card, Color.white); c.type = Image.Type.Sliced;
        Place(c.rectTransform, 0, 20, 1338, 853);
        Shadow(c.gameObject, 0.08f);
        var R = c.transform;
        var head = Img(R, "Header", pill, HeaderPink); head.type = Image.Type.Sliced;
        Place(head.rectTransform, 0, 312, 266, 92);
        Text(head.transform, "Text", "ผลลัพธ์", bold, 38, Black, 0, 0, 266, 92);

        Text(R, "TimeLabel", "เวลาที่ใช้ในโหมดนี้", regular, 34, Black, 0, 205, 600, 50);
        var time = Text(R, "Time", "00:00<size=40%>  นาที</size>", regular, 130, Black, 0, 100, 900, 150);
        time.richText = true;

        Text(R, "CyclesLabel", "หายใจเข้า-ออกครบ", regular, 32, Black, -250, -24, 440, 46);
        var cycles = Text(R, "Cycles", "0<size=40%>  รอบ</size>", regular, 84, Black, -250, -100, 440, 100);
        cycles.richText = true;
        Text(R, "ExhaleLabel", "เวลาที่เป่ารวม", regular, 32, Black, 250, -24, 440, 46);
        var exhale = Text(R, "Exhale", "0<size=40%>  วินาที</size>", regular, 84, Black, 250, -100, 440, 100);
        exhale.richText = true;
        var note = Text(R, "Note", "", regular, 26, Grey, 0, -175, 900, 40);

        var replay = PillButton(R, "ReplayButton", Pink, -134, -274, "Replay");
        var rl = replay.gameObject.AddComponent<SceneLoader>();
        UnityEventTools.AddVoidPersistentListener(replay.onClick, rl.ReloadCurrent);
        var home = PillButton(R, "HomeButton", Green, 134, -274, "Home");
        var hl = home.gameObject.AddComponent<SceneLoader>();
        UnityEventTools.AddStringPersistentListener(home.onClick, hl.LoadSceneByName, "Mode");   // หน้าหลัก = หน้าเลือกโหมด

        var ui = root.gameObject.AddComponent<SoundscapeResultUI>();
        ui.group = group;
        ui.card = c.rectTransform;
        ui.timeText = time;
        ui.cyclesText = cycles;
        ui.exhaleText = exhale;
        ui.noteText = note;
        root.SetAsLastSibling();
        root.gameObject.SetActive(false);
        BreezThaiFontInstaller.Apply(root.gameObject);
        return ui;
    }

    static Button PillButton(Transform parent, string name, Color color, float x, float y, string label)
    {
        var img = Img(parent, name, pill, color);
        img.type = Image.Type.Sliced;
        img.raycastTarget = true;
        Place(img.rectTransform, x, y, 238, 96);
        Shadow(img.gameObject, 0.10f);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        Text(img.transform, "Label", label, regular, 36, Color.white, 0, 0, 238, 96);
        return b;
    }

    static void Shadow(GameObject go, float alpha)
    {
        var s = go.AddComponent<Shadow>();
        s.effectColor = new Color(0, 0, 0, alpha);
        s.effectDistance = new Vector2(0, -6);
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
