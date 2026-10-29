using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================
// เมนู Breez > Create Mood Tracking Scene
// สร้าง Assets/Scenes/MoodTracking.unity ตามดีไซน์ Weekly Summary
// + สร้างรูป placeholder ใน Assets/element/Mood/
// + ผูกปุ่ม "Mood Tracking & Summary" ใน scene Mode ให้เปิดหน้านี้
// ถ้ามี TMP font ที่รองรับภาษาไทยในโปรเจกต์ จะใช้ข้อความภาษาไทยให้อัตโนมัติ
// =====================================================================
public static class BreezMoodSceneBuilder
{
    const string ScenePath = "Assets/Scenes/MoodTracking.unity";
    const string SceneName = "MoodTracking";
    const string ArtFolder = "Assets/element/Mood";
    const string ModeScenePath = "Assets/Scenes/Mode.unity";

    static readonly string[] Moods = { "Lovely", "Happy", "Bored", "Angry", "Stress" };

    // สีตามดีไซน์
    static readonly Color Bg = new Color32(243, 245, 236, 255);
    static readonly Color Panel = new Color32(253, 253, 248, 255);
    static readonly Color CardColor = Color.white;
    static readonly Color Ink = new Color32(30, 30, 30, 255);
    static readonly Color Muted = new Color32(70, 70, 70, 255);
    static readonly Color Green = new Color32(155, 184, 58, 255);
    static readonly Color RowBg = new Color32(0xFC, 0xFF, 0xE4, 255);
    static readonly Color RowTitle = new Color32(0x7F, 0x99, 0x84, 255);
    static readonly Color RowText = new Color32(110, 110, 95, 255);
    // ตามดีไซน์ Weekly Summary
    static readonly Color PanelColor = new Color32(0xFF, 0xFF, 0xFB, 255);
    static readonly Color TitleColor = new Color32(0xEF, 0xF1, 0xBC, 255);
    static readonly Color TitleInk = new Color32(0x6F, 0x7B, 0x52, 255);
    static readonly Color FrameGrey = new Color32(0xA4, 0xA4, 0xA4, 255);
    static readonly Color BackInk = new Color32(0x55, 0x55, 0x55, 255);

    static TMP_FontAsset font;
    static bool thai;
    static Sprite rounded, circle, flower, arrow;

    [MenuItem("Breez/Create Mood Tracking Scene")]
    public static void Create()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Breez", "MoodTracking.unity already exists. Replace it?", "Replace", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // ---------- รูป + font ----------
        var faces = new Dictionary<string, Sprite>();
        foreach (var m in Moods) faces[m] = MakeSprite($"mood_{m.ToLower()}", BreezSpriteArt.Face(m, 256), 256, 0);
        rounded = MakeSprite("ui_rounded", BreezSpriteArt.RoundedRect(128, 40), 128, 40);
        circle = MakeSprite("ui_circle", BreezSpriteArt.Circle(128), 128, 0);
        flower = MakeSprite("ui_flower", BreezSpriteArt.Flower(128), 128, 0);
        arrow = MakeSprite("ui_arrow_left", BreezSpriteArt.ArrowLeft(128), 128, 0);
        font = FindThaiFont();
        thai = font != null;

        // ---------- Scene ----------
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Bg;
        }

        var canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // พื้นหลัง + วงกลมฟุ้งๆ ตามดีไซน์
        var root = canvasGO.transform;

        // พื้นหลังครีม + ดอกไม้ฟุ้งๆ + เนินเขา (Assets/element/Mood/weekly_bg.png)
        var bgSprite = LoadSprite(ArtFolder + "/weekly_bg.png");
        var bg = Img("Background", root, bgSprite, bgSprite != null ? Color.white : Bg, 0, 0, 1920, 1080);
        bg.preserveAspect = false;
        Stretch(bg.rectTransform);

        // ปุ่มกลับ (pill ขาว)
        var pillSprite = BreezStatusElementsBuilder.MakeSprite("ui_pill", BreezSpriteArt.RoundedRect(128, 63), 128, 63);
        var back = Img("BackButton", root, pillSprite, Color.white, 120, 48, 243, 72);
        back.type = Image.Type.Sliced;
        back.preserveAspect = false;
        back.raycastTarget = true;
        var backOutline = back.gameObject.AddComponent<Outline>();
        backOutline.effectColor = new Color32(0xE6, 0xE6, 0xE2, 255);
        backOutline.effectDistance = new Vector2(1.5f, -1.5f);
        var backBtn = back.gameObject.AddComponent<Button>();
        backBtn.targetGraphic = back;
        var backArrow = Img("Arrow", back.transform, arrow, BackInk, 68, 23, 30, 26);
        backArrow.preserveAspect = true;
        Txt("Label", back.transform, "กลับ", 36, BackInk, FontStyles.Bold, 104, 8, 120, 56);

        // แผงใหญ่สีขาวครีม
        var panelImg = Card("Panel", root, PanelColor, 242, 163, 1436, 960, false).GetComponent<Image>();
        panelImg.pixelsPerUnitMultiplier = 0.55f;   // มุมโค้งใหญ่ตามดีไซน์

        var status = Txt("Status", root, "", 18, RowTitle, FontStyles.Normal, 381, 1038, 637, 30);
        status.alignment = TextAlignmentOptions.Center;

        // ---------- คอลัมน์ซ้าย ----------
        var title = Card("TitleCard", root, TitleColor, 381, 280, 637, 107);
        var t = Txt("Title", title, "สรุปรายสัปดาห์", 60, TitleInk, FontStyles.Bold, 0, 10, 637, 86);
        t.alignment = TextAlignmentOptions.Center;

        var moodCard = Card("MoodCard", root, CardColor, 381, 414, 637, 216);
        Txt("Question", moodCard, "อารมณ์ของคุณวันนี้เป็นยังไง??", 22, Muted, FontStyles.Normal, 36, 24, 440, 38);
        var date = Txt("Date", moodCard, "26/08/2026", 22, Muted, FontStyles.Normal, 420, 24, 182, 38);
        date.alignment = TextAlignmentOptions.MidlineRight;

        var moodButtons = new List<MoodTrackingScreen.MoodButton>();
        for (int i = 0; i < Moods.Length; i++)
        {
            float x = 36 + i * 119;
            var frame = Img($"Mood {Moods[i]}", moodCard, rounded, FrameGrey, x, 72, 91, 91);
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 6f;
            frame.preserveAspect = false;
            var inner = Img("Inner", frame.transform, rounded, Color.white, 2, 2, 87, 87);
            inner.type = Image.Type.Sliced;
            inner.pixelsPerUnitMultiplier = 6f;
            inner.preserveAspect = false;
            inner.raycastTarget = false;
            var icon = Img("Icon", frame.transform, faces[Moods[i]], Color.white, 16, 16, 59, 59);
            icon.raycastTarget = false;
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            var label = Txt("Label", moodCard, Moods[i].ToLower(), 17, Muted, FontStyles.Normal, x - 10, 168, 111, 28);
            label.alignment = TextAlignmentOptions.Center;
            moodButtons.Add(new MoodTrackingScreen.MoodButton { mood = Moods[i], button = btn, frame = frame, icon = icon });
        }

        var sessCard = Card("SessionsCard", root, CardColor, 381, 653, 312, 155);
        Txt("Label", sessCard, "Sessions", 22, Muted, FontStyles.Normal, 40, 22, 240, 34);
        var sessions = Txt("Value", sessCard, "<size=80>0</size>  <size=22>Sessions</size>", 22, Ink, FontStyles.Normal, 40, 56, 260, 92);

        var moodAvg = Card("AverageMoodCard", root, CardColor, 705, 653, 313, 155);
        Txt("Label", moodAvg, "อารมณ์เฉลี่ย", 22, Muted, FontStyles.Normal, 40, 22, 200, 34);
        var seeAllMood = SeeAll(moodAvg, 196, 22);
        var avgIcon = Img("Icon", moodAvg, faces["Lovely"], Color.white, 36, 64, 58, 58);
        var avgText = Txt("Value", moodAvg, "—", 22, Ink, FontStyles.Normal, 113, 74, 180, 40);

        var modes = Card("GamesCard", root, CardColor, 381, 830, 637, 154);
        Txt("Label", modes, "Sessions", 22, Muted, FontStyles.Normal, 40, 22, 400, 34);
        var calm = Txt("CalmFlow", modes, "", 20, Ink, FontStyles.Normal, 40, 54, 190, 88);
        var power = Txt("PowerRelease", modes, "", 20, Ink, FontStyles.Normal, 214, 54, 210, 88);
        var focus = Txt("BreatheFocus", modes, "", 20, Ink, FontStyles.Normal, 412, 54, 220, 88);
        foreach (var label in new[] { calm, power, focus, sessions })
        {
            label.alignment = TextAlignmentOptions.BottomLeft;
            label.enableWordWrapping = false; // ตัวเลขใหญ่ + ชื่อเกมอยู่บรรทัดเดียว
        }

        // ---------- คอลัมน์ขวา ----------
        var streakCard = Card("StreakCard", root, CardColor, 1053, 288, 476, 164);
        Img("Flower Green", streakCard, flower, new Color32(0xD5, 0xE7, 0x9A, 255), 336, 10, 88, 88);
        Img("Flower Yellow", streakCard, flower, new Color32(0xEF, 0xF1, 0xBC, 255), 313, 96, 54, 54);
        Img("Flower Pink", streakCard, flower, new Color32(0xF7, 0xC6, 0xD0, 255), 402, 99, 38, 38);
        Txt("Label", streakCard, "streak", 22, Muted, FontStyles.Normal, 40, 22, 200, 34);
        var streak = Txt("Value", streakCard, "", 26, Ink, FontStyles.Normal, 40, 50, 260, 104);
        streak.alignment = TextAlignmentOptions.BottomLeft;
        streak.enableWordWrapping = false;

        var recent = Card("RecentCard", root, CardColor, 1053, 475, 476, 509);
        Txt("Label", recent, "เล่นล่าสุด", 22, Muted, FontStyles.Normal, 36, 20, 300, 34);
        var seeAllRecent = SeeAll(recent, 360, 20);
        var empty = Txt("Empty", recent, "ยังไม่มีการเล่นในสัปดาห์นี้", 20, RowText, FontStyles.Normal, 30, 200, 416, 40);
        empty.alignment = TextAlignmentOptions.Center;

        var rows = new List<MoodTrackingScreen.RecentRow>();
        for (int i = 0; i < 3; i++)
        {
            var row = Card($"Row {i + 1}", recent, RowBg, 29, 65 + i * 105, 413, 90);
            var rt = Txt("Title", row, "Breathe & Focus", 25, RowTitle, FontStyles.Bold, 22, 8, 290, 36);
            var rd = Txt("Detail", row, "", 13, RowText, FontStyles.Normal, 22, 44, 300, 40);
            rd.alignment = TextAlignmentOptions.TopLeft;
            var rs = Txt("Score", row, "0", 28, RowText, FontStyles.Normal, 290, 8, 100, 40);
            rs.alignment = TextAlignmentOptions.MidlineRight;
            var rl = Txt("ScoreLabel", row, "คะแนน", 15, RowText, FontStyles.Normal, 290, 50, 100, 26);
            rl.alignment = TextAlignmentOptions.MidlineRight;
            rows.Add(new MoodTrackingScreen.RecentRow { root = row.gameObject, title = rt, detail = rd, score = rs });
        }

        // ---------- ต่อสาย ----------
        var screen = canvasGO.AddComponent<MoodTrackingScreen>();
        screen.backScene = "Mode";
        screen.backButton = backBtn;
        screen.dateText = date;
        screen.statusText = status;
        screen.moodButtons = moodButtons.ToArray();
        screen.sessionsText = sessions;
        screen.topMoodIcon = avgIcon;
        screen.topMoodText = avgText;
        screen.calmCountText = calm;
        screen.powerCountText = power;
        screen.focusCountText = focus;
        screen.streakText = streak;
        screen.recentRows = rows.ToArray();
        screen.emptyRecentText = empty;
        screen.seeAllButtons = new[] { seeAllMood, seeAllRecent };

        var account = canvasGO.AddComponent<AccountActions>();
        account.requireLogin = true;

        // หน้าต่างประวัติรายวัน (เปิดจากปุ่ม See All)
        BreezDailyHistoryBuilder.Build(screen, addButton: false);

        foreach (var sceneRoot in scene.GetRootGameObjects()) BreezThaiFontInstaller.Apply(sceneRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);

        string wired = WireModeButton();
        EditorSceneManager.OpenScene(ScenePath);

        EditorUtility.DisplayDialog("Breez",
            "Created Assets/Scenes/MoodTracking.unity\n\n" + wired +
            (thai ? "\n\nThai font found: " + font.name : "\n\nNo Thai font found, using English text. Add a Thai TMP font and run this again for Thai labels."),
            "OK");
    }

    // ---------------------------------------------------------------
    // ผูกปุ่มใน scene Mode
    // ---------------------------------------------------------------
    static string WireModeButton()
    {
        if (!File.Exists(ModeScenePath)) return "Mode scene not found — add a button that loads \"MoodTracking\" yourself.";
        var mode = EditorSceneManager.OpenScene(ModeScenePath, OpenSceneMode.Single);

        Button target = null;
        foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
            if (b.gameObject.scene == mode && b.gameObject.name.StartsWith("Mood Tracking")) { target = b; break; }
        var loader = Object.FindObjectOfType<SceneLoader>();

        if (target == null || loader == null)
            return "Could not find the \"Mood Tracking & Summary\" button in Mode — set its OnClick to SceneLoader.LoadSceneByName(\"MoodTracking\").";

        while (target.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(target.onClick, 0);
        UnityEventTools.AddStringPersistentListener(target.onClick, new UnityAction<string>(loader.LoadSceneByName), SceneName);
        EditorUtility.SetDirty(target);
        EditorSceneManager.MarkSceneDirty(mode);
        EditorSceneManager.SaveScene(mode);
        return "The \"Mood Tracking & Summary\" button in Mode now opens this scene.";
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------
    static TMP_FontAsset FindThaiFont()
    {
        var breez = BreezThaiFontInstaller.EnsureFonts();
        if (breez != null) return breez;
        foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (f != null && f.HasCharacter('ก', false, true)) return f;
        }
        return null;
    }

    static Sprite MakeSprite(string name, float[] rgba, int size, int border)
    {
        if (!AssetDatabase.IsValidFolder(ArtFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/element")) AssetDatabase.CreateFolder("Assets", "element");
            AssetDatabase.CreateFolder("Assets/element", "Mood");
        }
        string path = $"{ArtFolder}/{name}.png";

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite LoadSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>ลิงก์ "See All" ขีดเส้นใต้ มุมขวาบนของการ์ด (x,y จากซ้ายบน)</summary>
    static Button SeeAll(Transform card, float x, float y)
    {
        var t = Txt("SeeAll", card, "<u>See All</u>", 18, Muted, FontStyles.Normal, x, y, 90, 34);
        t.alignment = TextAlignmentOptions.MidlineRight;
        t.raycastTarget = true;
        var b = t.gameObject.AddComponent<Button>();
        b.targetGraphic = t;
        return b;
    }

    static void AddToBuildSettings(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Exists(s => s.path == path)) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>วาง RectTransform ด้วยพิกัดจากมุมซ้ายบนของ parent (หน่วย px ที่ 1920x1080)</summary>
    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static Image Img(string name, Transform parent, Sprite sprite, Color color, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = sprite != null && sprite != rounded;
        Place(img.rectTransform, x, y, w, h);
        if (sprite == null || sprite == circle || sprite == flower) img.raycastTarget = false;
        return img;
    }

    static Transform Card(string name, Transform parent, Color color, float x, float y, float w, float h, bool shadow = true)
    {
        var img = Img(name, parent, rounded, color, x, y, w, h);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1.4f;
        img.raycastTarget = false;
        if (shadow)
        {
            var s = img.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, 0.08f);
            s.effectDistance = new Vector2(0, -5);
        }
        return img.transform;
    }

    static TextMeshProUGUI Txt(string name, Transform parent, string text, float size, Color color, FontStyles style,
                               float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        Place(t.rectTransform, x, y, w, h);
        return t;
    }
}
