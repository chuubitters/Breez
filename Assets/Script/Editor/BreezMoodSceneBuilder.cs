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
    static readonly Color RowBg = new Color32(251, 249, 221, 255);
    static readonly Color RowTitle = new Color32(126, 156, 106, 255);
    static readonly Color RowText = new Color32(110, 110, 95, 255);

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
        var bg = Img("Background", root, null, Bg, 0, 0, 1920, 1080);
        Stretch(bg.rectTransform);
        Img("Blob Yellow", root, circle, new Color32(250, 226, 160, 70), 120, 300, 260, 260);
        Img("Blob Blue", root, circle, new Color32(176, 214, 230, 80), -60, 480, 380, 380);
        Img("Blob Green", root, circle, new Color32(214, 230, 170, 90), 1560, 520, 460, 460);
        Img("Blob Green 2", root, flower, new Color32(220, 236, 190, 255), 205, 800, 150, 150);

        // ปุ่ม Back
        var back = Img("BackButton", root, null, new Color(1, 1, 1, 0), 110, 55, 260, 80);
        var backBtn = back.gameObject.AddComponent<Button>();
        Img("Arrow", back.transform, arrow, Green, 8, 22, 44, 36);
        Txt("Label", back.transform, "Back", 54, Green, FontStyles.Bold, 62, 4, 190, 70);

        // แผงใหญ่
        Card("Panel", root, Panel, 242, 161, 1436, 960, false);

        var status = Txt("Status", root, "", 18, RowTitle, FontStyles.Normal, 381, 1038, 637, 30);
        status.alignment = TextAlignmentOptions.Center;

        // ---------- คอลัมน์ซ้าย ----------
        var title = Card("TitleCard", root, CardColor, 381, 288, 637, 101);
        var t = Txt("Title", title, "Weekly Summary", 56, Ink, FontStyles.Bold, 0, 12, 637, 80);
        t.alignment = TextAlignmentOptions.Center;

        var moodCard = Card("MoodCard", root, CardColor, 381, 415, 637, 216);
        Txt("Question", moodCard, thai ? "อารมณ์ของคุณวันนี้เป็นยังไง??" : "How are you feeling today?", 21, Muted, FontStyles.Normal, 36, 26, 420, 36);
        var date = Txt("Date", moodCard, "26/08/2026", 21, Muted, FontStyles.Normal, 430, 26, 172, 36);
        date.alignment = TextAlignmentOptions.MidlineRight;

        var moodButtons = new List<MoodTrackingScreen.MoodButton>();
        for (int i = 0; i < Moods.Length; i++)
        {
            float x = 36 + i * 119;
            var frame = Img($"Mood {Moods[i]}", moodCard, rounded, new Color32(226, 228, 220, 255), x, 70, 91, 91);
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 4f;
            var inner = Img("Inner", frame.transform, rounded, Color.white, 3, 3, 85, 85);
            inner.type = Image.Type.Sliced;
            inner.pixelsPerUnitMultiplier = 4f;
            inner.raycastTarget = false;
            var icon = Img("Icon", frame.transform, faces[Moods[i]], Color.white, 16, 16, 59, 59);
            icon.raycastTarget = false;
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            var label = Txt("Label", moodCard, Moods[i].ToLower(), 17, Muted, FontStyles.Normal, x - 10, 166, 111, 28);
            label.alignment = TextAlignmentOptions.Center;
            moodButtons.Add(new MoodTrackingScreen.MoodButton { mood = Moods[i], button = btn, frame = frame, icon = icon });
        }

        var sessCard = Card("SessionsCard", root, CardColor, 381, 645, 312, 163);
        Txt("Label", sessCard, "Sessions", 22, Muted, FontStyles.Normal, 40, 22, 240, 34);
        var sessions = Txt("Value", sessCard, "<size=80>0</size>  <size=22>Sessions</size>", 22, Ink, FontStyles.Normal, 40, 58, 260, 92);
        sessions.alignment = TextAlignmentOptions.BottomLeft;

        var moodAvg = Card("AverageMoodCard", root, CardColor, 705, 645, 313, 163);
        Txt("Label", moodAvg, thai ? "อารมณ์ที่พบบ่อย" : "Average Mood", 22, Muted, FontStyles.Normal, 40, 22, 250, 34);
        var avgIcon = Img("Icon", moodAvg, faces["Lovely"], Color.white, 36, 68, 62, 62);
        var avgText = Txt("Value", moodAvg, "—", 24, Ink, FontStyles.Normal, 120, 80, 180, 40);

        var modes = Card("GamesCard", root, CardColor, 381, 828, 637, 156);
        Txt("Label", modes, thai ? "จำนวนครั้งแต่ละเกม" : "Sessions by game", 22, Muted, FontStyles.Normal, 40, 22, 400, 34);
        var calm = Txt("CalmFlow", modes, "", 20, Ink, FontStyles.Normal, 40, 56, 200, 88);
        var power = Txt("PowerRelease", modes, "", 20, Ink, FontStyles.Normal, 230, 56, 210, 88);
        var focus = Txt("BreatheFocus", modes, "", 20, Ink, FontStyles.Normal, 430, 56, 200, 88);
        foreach (var label in new[] { calm, power, focus, sessions })
        {
            label.alignment = TextAlignmentOptions.BottomLeft;
            label.enableWordWrapping = false; // ตัวเลขใหญ่ + ชื่อเกมอยู่บรรทัดเดียว
        }

        // ---------- คอลัมน์ขวา ----------
        var streakCard = Card("StreakCard", root, new Color32(250, 252, 255, 255), 1053, 285, 476, 168);
        Img("Flower Blue", streakCard, flower, new Color32(206, 230, 238, 255), 352, -8, 118, 118);
        Img("Flower Yellow", streakCard, flower, new Color32(250, 226, 160, 255), 282, 108, 72, 72);
        Img("Flower Green", streakCard, flower, new Color32(200, 226, 140, 255), 390, 96, 56, 56);
        Txt("Label", streakCard, "streak", 22, Muted, FontStyles.Normal, 40, 26, 200, 34);
        var streak = Txt("Value", streakCard, "", 26, Ink, FontStyles.Normal, 40, 54, 260, 104);
        streak.alignment = TextAlignmentOptions.BottomLeft;
        streak.enableWordWrapping = false;

        var recent = Card("RecentCard", root, CardColor, 1053, 470, 476, 514);
        Txt("Label", recent, "Recent Play", 22, Muted, FontStyles.Normal, 36, 22, 300, 34);
        var empty = Txt("Empty", recent, thai ? "ยังไม่มีการเล่นในสัปดาห์นี้" : "No games this week yet", 20, RowText, FontStyles.Normal, 30, 200, 416, 40);
        empty.alignment = TextAlignmentOptions.Center;

        var rows = new List<MoodTrackingScreen.RecentRow>();
        for (int i = 0; i < 4; i++)
        {
            var row = Card($"Row {i + 1}", recent, RowBg, 30, 70 + i * 106, 416, 92);
            var rt = Txt("Title", row, "Breathe & Focus", 24, RowTitle, FontStyles.Bold, 22, 10, 290, 34);
            var rd = Txt("Detail", row, "", 13, RowText, FontStyles.Normal, 22, 44, 300, 42);
            rd.alignment = TextAlignmentOptions.TopLeft;
            var rs = Txt("Score", row, "0", 30, RowText, FontStyles.Normal, 300, 8, 96, 40);
            rs.alignment = TextAlignmentOptions.MidlineRight;
            var rl = Txt("ScoreLabel", row, thai ? "คะแนน" : "points", 14, RowText, FontStyles.Normal, 300, 50, 96, 24);
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

        var account = canvasGO.AddComponent<AccountActions>();
        account.requireLogin = true;

        // ปุ่ม "ดูประวัติ" + หน้าต่างประวัติรายวัน
        BreezDailyHistoryBuilder.Build(screen);

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
