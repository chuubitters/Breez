using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// Breez > Add Daily History To Mood Tracking
// ใส่ปุ่ม "ดูประวัติ" + หน้าต่างประวัติรายวัน ลงใน MoodTracking.unity ที่มีอยู่
// (ไม่สร้าง scene ใหม่ ของที่ปรับไว้เองยังอยู่ครบ) กดซ้ำได้
// เมนู Create Mood Tracking Scene ก็เรียกส่วนนี้ให้อัตโนมัติ
// =====================================================================
public static class BreezDailyHistoryBuilder
{
    const string ScenePath = "Assets/Scenes/MoodTracking.unity";
    const string MoodArt = "Assets/element/Mood";
    static readonly string[] Moods = { "Lovely", "Happy", "Bored", "Angry", "Stress" };

    static readonly Color Ink = new Color32(51, 51, 51, 255);
    static readonly Color Grey = new Color32(120, 120, 120, 255);
    static readonly Color Line = new Color32(0xE8, 0xEA, 0xE0, 255);
    static readonly Color Green = new Color32(0xA9, 0xC7, 0x7E, 255);
    static readonly Color GreenText = new Color32(0x7E, 0x9C, 0x6A, 255);
    static readonly Color RowMood = new Color32(0xFD, 0xF3, 0xF5, 255);
    static readonly Color RowPlay = new Color32(0xFB, 0xF9, 0xDD, 255);

    static TMP_FontAsset regular, bold;
    static Sprite pill, card, circle;

    [MenuItem("Breez/Add Daily History To Mood Tracking")]
    public static void AddMenu()
    {
        if (!File.Exists(ScenePath)) { EditorUtility.DisplayDialog("Breez", "ไม่พบ " + ScenePath + "\nสร้างด้วย Breez > Create Mood Tracking Scene ก่อน", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var screen = Object.FindObjectOfType<MoodTrackingScreen>();
        if (screen == null) { EditorUtility.DisplayDialog("Breez", "ไม่พบ MoodTrackingScreen ใน scene", "OK"); return; }
        Build(screen);
        foreach (var r in scene.GetRootGameObjects()) BreezThaiFontInstaller.Apply(r);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Breez", "เพิ่มปุ่ม \"ดูประวัติ\" ในหน้า Weekly Summary แล้ว\n\nกดเพื่อดูประวัติการเล่นและอารมณ์ของแต่ละวัน เลื่อนดูทีละสัปดาห์ได้", "OK");
    }

    /// <summary>สร้างปุ่ม + หน้าต่างประวัติ แล้วผูกกับ MoodTrackingScreen</summary>
    public static void Build(MoodTrackingScreen screen)
    {
        regular = BreezThaiFontInstaller.EnsureFonts();
        bold = BreezThaiFontInstaller.BoldFont;
        if (bold == null) bold = regular;
        pill = BreezStatusElementsBuilder.MakeSprite("ui_pill", BreezSpriteArt.RoundedRect(128, 63), 128, 63);
        card = BreezStatusElementsBuilder.MakeSprite("ui_card", BreezSpriteArt.RoundedRect(128, 40), 128, 40);
        circle = BreezStatusElementsBuilder.MakeSprite("ui_circle", BreezSpriteArt.Circle(256), 256, 0);

        var canvas = screen.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindObjectOfType<Canvas>();
        var C = canvas.rootCanvas.transform;
        Undo.RecordObject(screen, "Daily history");

        // ---------- ปุ่มดูประวัติ ----------
        Transform host = FindDeep(C, "RecentCard");
        var oldBtn = host != null ? host.Find("HistoryButton") : C.Find("HistoryButton");
        if (oldBtn != null) Object.DestroyImmediate(oldBtn.gameObject);
        var btnImg = Img(host != null ? host : C, "HistoryButton", pill, Green);
        btnImg.type = Image.Type.Sliced;
        btnImg.raycastTarget = true;
        var brt = btnImg.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(1f, 1f);
        brt.sizeDelta = new Vector2(150, 44);
        brt.anchoredPosition = host != null ? new Vector2(-24, -18) : new Vector2(-60, -40);
        var btn = btnImg.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var bl = Text(btnImg.transform, "Label", "ดูประวัติ", bold, 22, Color.white, 0, 0, 150, 44);
        Stretch(bl.rectTransform);
        screen.historyButton = btn;

        // ---------- หน้าต่างประวัติ ----------
        var old = C.Find("DailyHistory");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var holder = UI("DailyHistory", C); Stretch(holder);
        holder.SetAsLastSibling();
        var hp = holder.gameObject.AddComponent<DailyHistoryPanel>();

        var dim = Img(holder, "Panel", null, new Color(0.13f, 0.16f, 0.1f, 0.35f));
        dim.raycastTarget = true;
        Stretch(dim.rectTransform);
        hp.panel = dim.gameObject;

        var box = Img(dim.transform, "Card", card, Color.white);
        box.type = Image.Type.Sliced;
        box.raycastTarget = true;
        Place(box.rectTransform, 0, 0, 1400, 920);
        var B = box.transform;

        Left(Text(B, "Title", "ประวัติย้อนหลัง", bold, 44, Ink, -650, 400, 600, 64));
        var close = Button(B, "CloseButton", circle, new Color32(0xF3, 0xF4, 0xEE, 255), 640, 400, 68, 68, "×", Ink, 44);
        hp.closeButton = close;

        // แถบเลือกสัปดาห์
        // ฟอนต์ไม่มี ‹ › จึงใช้รูปลูกศร
        var arrow = BreezStatusElementsBuilder.MakeSprite("ui_arrow_left", BreezSpriteArt.ArrowLeft(128), 128, 0);
        hp.prevButton = Button(B, "PrevWeek", circle, new Color32(0xF3, 0xF4, 0xEE, 255), -220, 318, 60, 60, "", Ink, 40);
        hp.nextButton = Button(B, "NextWeek", circle, new Color32(0xF3, 0xF4, 0xEE, 255), 220, 318, 60, 60, "", Ink, 40);
        var pa = Img(hp.prevButton.transform, "Arrow", arrow, Ink); Place(pa.rectTransform, 0, 0, 26, 26);
        var na = Img(hp.nextButton.transform, "Arrow", arrow, Ink); Place(na.rectTransform, 0, 0, 26, 26);
        na.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
        hp.rangeText = Text(B, "Range", "21 – 27 ต.ค. 2026", bold, 30, Ink, 0, 318, 360, 50);

        // วัน 7 วัน
        var chips = new List<DailyHistoryPanel.DayChip>();
        for (int i = 0; i < 7; i++)
        {
            float x = -570 + i * 190;
            var chipImg = Img(B, "Day" + i, card, Color.white);
            chipImg.type = Image.Type.Sliced;
            chipImg.raycastTarget = true;
            Place(chipImg.rectTransform, x, 190, 170, 150);
            var outline = chipImg.gameObject.AddComponent<Outline>();
            outline.effectColor = Line; outline.effectDistance = new Vector2(2, -2);
            var b = chipImg.gameObject.AddComponent<Button>();
            b.targetGraphic = chipImg;
            var cb = b.colors; cb.disabledColor = new Color(1, 1, 1, 0.6f); b.colors = cb;
            var wd = Text(chipImg.transform, "Weekday", "จ.", regular, 22, Grey, 0, 46, 160, 32);
            var dn = Text(chipImg.transform, "Day", "21", bold, 50, Ink, 0, 2, 160, 60);
            var dot = Img(chipImg.transform, "MoodDot", circle, Color.white); Place(dot.rectTransform, -38, -46, 18, 18);
            var cnt = Text(chipImg.transform, "Count", "", regular, 18, GreenText, 14, -46, 110, 28);
            chips.Add(new DailyHistoryPanel.DayChip { button = b, background = chipImg, weekday = wd, day = dn, moodDot = dot, count = cnt });
        }
        hp.chips = chips.ToArray();

        hp.dayTitleText = Left(Text(B, "DayTitle", "วันอังคารที่ 27 ต.ค. 2026", bold, 30, Ink, -650, 72, 900, 46));
        hp.statusText = Text(B, "Status", "", regular, 20, Grey, 450, 72, 400, 40);

        // คอลัมน์อารมณ์ / การเล่น
        Left(Text(B, "MoodHeader", "อารมณ์", bold, 26, GreenText, -650, 18, 300, 40));
        Left(Text(B, "PlayHeader", "การเล่น", bold, 26, GreenText, -70, 18, 300, 40));
        hp.moodContent = List(B, "MoodList", -650 + 270, -205, 540, 400, out var moodEmptyParent);
        hp.sessionContent = List(B, "PlayList", -70 + 360, -205, 720, 400, out var playEmptyParent);
        hp.moodEmpty = Text(moodEmptyParent, "Empty", "ไม่ได้บันทึกอารมณ์ในวันนี้", regular, 22, Grey, 0, 0, 500, 40);
        hp.sessionEmpty = Text(playEmptyParent, "Empty", "ไม่มีการเล่นในวันนี้", regular, 22, Grey, 0, 0, 680, 40);

        hp.moodTemplate = MoodRow(hp.moodContent);
        hp.sessionTemplate = SessionRow(hp.sessionContent);

        var icons = new List<DailyHistoryPanel.MoodIcon>();
        foreach (var m in Moods)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>($"{MoodArt}/mood_{m.ToLower()}.png");
            if (sp != null) icons.Add(new DailyHistoryPanel.MoodIcon { mood = m, sprite = sp });
        }
        hp.moodIcons = icons.ToArray();

        dim.gameObject.SetActive(false);
        screen.historyPanel = hp;
        EditorUtility.SetDirty(screen);
    }

    // ---------------------------------------------------------------
    static RectTransform List(Transform parent, string name, float x, float y, float w, float h, out Transform emptyParent)
    {
        var scroll = UI(name, parent);
        Place(scroll, x, y, w, h);
        var sr = scroll.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f;

        var viewport = UI("Viewport", scroll); Stretch(viewport);
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(1, 1, 1, 0.001f);   // ให้ลากเลื่อนได้
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = UI("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, 0);
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 12;
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = viewport;
        sr.content = content;
        emptyParent = scroll;
        return content;
    }

    static GameObject MoodRow(Transform content)
    {
        var row = Img(content, "MoodRowTemplate", card, RowMood);
        row.type = Image.Type.Sliced;
        row.rectTransform.sizeDelta = new Vector2(0, 96);
        var R = row.transform;
        var dot = Img(R, "Dot", circle, Color.white); PlaceLeft(dot.rectTransform, 18, 0, 64, 64);
        var icon = Img(R, "Icon", null, Color.white); PlaceLeft(icon.rectTransform, 22, 0, 56, 56);
        icon.preserveAspect = true;
        Left(Text(R, "Name", "มีความสุข", bold, 26, Ink, 0, 16, 300, 38), true, 100);
        Left(Text(R, "Note", "", regular, 18, Grey, 0, -18, 400, 30), true, 100);
        var time = Text(R, "Time", "14:05 น.", regular, 20, Grey, 0, 16, 140, 32);
        Right(time, -20);
        return row.gameObject;
    }

    static GameObject SessionRow(Transform content)
    {
        var row = Img(content, "PlayRowTemplate", card, RowPlay);
        row.type = Image.Type.Sliced;
        row.rectTransform.sizeDelta = new Vector2(0, 112);
        var R = row.transform;
        Left(Text(R, "Mode", "Breathe & Focus", bold, 26, GreenText, 0, 30, 420, 38), true, 24);
        Left(Text(R, "Detail", "ระยะเวลา 01:20 นาที", regular, 18, Grey, 0, -14, 480, 56), true, 24).alignment = TextAlignmentOptions.TopLeft;
        var time = Text(R, "Time", "14:05 น.", regular, 18, Grey, 0, 34, 140, 28); Right(time, -150);
        var score = Text(R, "Score", "0", bold, 40, Ink, 0, 4, 120, 52); Right(score, -22);
        var label = Text(R, "ScoreLabel", "คะแนน", regular, 16, Grey, 0, -32, 120, 26); Right(label, -22);
        return row.gameObject;
    }

    // ---------------------------------------------------------------
    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root) { var f = FindDeep(c, name); if (f != null) return f; }
        return null;
    }

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
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    static Button Button(Transform parent, string name, Sprite sprite, Color color, float x, float y, float w, float h,
                         string label, Color textColor, float size)
    {
        var img = Img(parent, name, sprite, color);
        img.raycastTarget = true;
        Place(img.rectTransform, x, y, w, h);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var t = Text(img.transform, "Label", label, bold, size, textColor, 0, 0, w, h);
        Stretch(t.rectTransform);
        return b;
    }

    /// <summary>จัดข้อความชิดซ้าย: x = ขอบซ้าย (ในระบบพิกัดกลาง) หรือ inRow = ชิดซ้ายของแถวเว้น leftPad</summary>
    static TextMeshProUGUI Left(TextMeshProUGUI t, bool inRow = false, float leftPad = 0f)
    {
        var rt = t.rectTransform;
        t.alignment = TextAlignmentOptions.Left;
        if (inRow)
        {
            float y = rt.anchoredPosition.y; var size = rt.sizeDelta;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(leftPad, y);
        }
        else
        {
            rt.pivot = new Vector2(0f, 0.5f);
        }
        return t;
    }

    static void Right(TextMeshProUGUI t, float x)
    {
        var rt = t.rectTransform;
        float y = rt.anchoredPosition.y;
        t.alignment = TextAlignmentOptions.Right;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
    }

    static void PlaceLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
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
