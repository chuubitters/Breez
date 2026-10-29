using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================
// Breez > Create Calm Flow Scene
//   สร้าง Assets/Scenes/CalmFlow.unity (ใช้ PowerRelease เป็นต้นแบบเพื่อเอา SerialController/ESP32 มาด้วย)
//   • เลือกเวลา 1/2/3 นาที -> READY TO BLOOM -> Start Session
//   • เป่า = ดอกไม้ค่อยๆ บาน (BloomFlower), หยุดเป่า = ปลูกลงสวนด้านล่าง แล้วเริ่มดอกใหม่
//   • Pause / Resume / End Session
//   • สรุปผล: Flowers Grown / Largest Flower / Longest Exhale / Total Exhale Time
//   • ผูกปุ่ม "CalmFLow" ในหน้า BreathPlay ให้เข้าเกมนี้ + ใส่ใน Build Settings
// กดซ้ำได้ (สร้างใหม่ทับ)
// =====================================================================
public static class BreezCalmFlowBuilder
{
    const string TemplatePath = "Assets/Scenes/PowerRelease.unity";
    const string ScenePath = "Assets/Scenes/CalmFlow.unity";
    const string SceneName = "CalmFlow";
    const string BreathPlayPath = "Assets/Scenes/BreathPlay.unity";
    const string BgPath = "Assets/element/Breath Play/BreathPlay-BG.png";
    const string BackArtPath = "Assets/element/back-bt.png";
    const string BreathPrefab = "Assets/Prefabs/Breez/BreathStatus.prefab";

    static readonly Color Ink = new Color32(51, 51, 51, 255);
    static readonly Color Grey = new Color32(112, 112, 112, 255);
    static readonly Color Sage = new Color32(0x8F, 0xAE, 0x6A, 255);
    static readonly Color Pink = new Color32(0xF2, 0xBD, 0xC9, 255);
    static readonly Color PinkSoft = new Color32(0xFC, 0xE3, 0xE8, 255);
    static readonly Color Green = new Color32(0xD4, 0xE5, 0x9D, 255);
    static readonly Color Tile = new Color32(0xF8, 0xF6, 0xEE, 255);
    static readonly Color Stem = new Color32(0xA9, 0xC7, 0x7E, 255);
    static readonly Color Leaf = new Color32(0xB6, 0xD3, 0x8C, 255);
    static readonly Color Ground = new Color32(0xDD, 0xEB, 0xC2, 255);

    static TMP_FontAsset regular, bold;
    static Sprite pill, card, circle, petal, leaf, glow;

    [MenuItem("Breez/Create Calm Flow Scene")]
    public static void Create()
    {
        if (!File.Exists(TemplatePath)) { EditorUtility.DisplayDialog("Breez", "ไม่พบ " + TemplatePath + " (ใช้เป็นต้นแบบ)", "OK"); return; }
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Breez", "CalmFlow.unity มีอยู่แล้ว สร้างใหม่ทับไหม?", "สร้างใหม่", "ยกเลิก")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        regular = BreezThaiFontInstaller.EnsureFonts();
        bold = BreezThaiFontInstaller.BoldFont;
        if (bold == null) bold = regular;
        pill = BreezStatusElementsBuilder.MakeSprite("ui_pill", BreezSpriteArt.RoundedRect(128, 63), 128, 63);
        card = BreezStatusElementsBuilder.MakeSprite("ui_card", BreezSpriteArt.RoundedRect(128, 40), 128, 40);
        circle = BreezStatusElementsBuilder.MakeSprite("ui_circle", BreezSpriteArt.Circle(256), 256, 0);
        petal = BreezStatusElementsBuilder.MakeSprite("cf_petal", BreezSpriteArt.Petal(256), 256, 0);
        leaf = BreezStatusElementsBuilder.MakeSprite("cf_leaf", BreezSpriteArt.BloomLeaf(256), 256, 0);
        glow = BreezStatusElementsBuilder.MakeSprite("cf_glow", BreezSpriteArt.Glow(256), 256, 0);

        // ---------- scene จากต้นแบบ: เก็บกล้อง / EventSystem / SerialController / BreathReceiver ----------
        if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
        AssetDatabase.CopyAsset(TemplatePath, ScenePath);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (var root in scene.GetRootGameObjects())
        {
            bool keep = root.GetComponent<Camera>() != null || root.GetComponent<EventSystem>() != null ||
                        root.GetComponent<BreathReceiver>() != null;
            foreach (var mb in root.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == "SerialController") keep = true;
            if (!keep) Object.DestroyImmediate(root);
        }

        var receiver = Object.FindObjectOfType<BreathReceiver>();
        GameObject gmGO;
        if (receiver != null) gmGO = receiver.gameObject;
        else { gmGO = new GameObject("GameManager"); receiver = gmGO.AddComponent<BreathReceiver>(); }
        gmGO.name = "GameManager";
        int threshold = 50;
        var pw = gmGO.GetComponent<PWGameManager>();
        if (pw != null) { threshold = pw.breathThreshold; Object.DestroyImmediate(pw); }
        var game = gmGO.AddComponent<CalmFlowGame>();
        game.breathReceiver = receiver;
        game.breathThreshold = threshold;

        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // ---------- Canvas ----------
        var canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        var C = canvasGO.transform;

        var bg = Img(C, "Background", AssetDatabase.LoadAssetAtPath<Sprite>(BgPath), new Color32(0xF7, 0xFA, 0xEC, 255));
        Stretch(bg.rectTransform);
        var ground = Img(C, "Ground", circle, Ground);
        Place(ground.rectTransform, 0, -80, 2800, 520, 0.5f, 0f, 0.5f, 0.5f);

        // ---------- สวน + ดอกไม้ ----------
        var garden = UI("Garden", C);
        Place(garden, 0, 18, 1800, 300, 0.5f, 0f, 0.5f, 0f);
        game.garden = garden;
        game.flower = BuildFlower(C);

        // ---------- ปุ่มกลับ ----------
        var backArt = AssetDatabase.LoadAssetAtPath<Sprite>(BackArtPath);
        var back = Button(C, "BackButton", backArt != null ? backArt : pill, Color.white, -744, 424, 249, 79, backArt != null ? "" : "< กลับ", Ink, 30);
        if (backArt != null) back.GetComponent<Image>().preserveAspect = true;
        LoadScene(back, "BreathPlay");

        // ---------- หน้าเลือกเวลา ----------
        BuildSelect(C, game);

        // ---------- HUD ----------
        var hud = UI("HUD", C); Stretch(hud);
        game.hud = hud.gameObject;
        var timerPill = Img(hud, "TimerPill", pill, Color.white);
        timerPill.type = Image.Type.Sliced;
        Place(timerPill.rectTransform, 0, 413, 234, 91);
        AddShadow(timerPill.gameObject);
        game.timerText = Text(timerPill.transform, "Timer", "02:00", bold, 44, Ink, 0, 0, 234, 91);
        game.hintText = Text(hud, "Hint", "ค่อยๆ ผ่อนลมหายใจออก", regular, 32, Grey, 0, 215, 1000, 50);
        game.bloomText = Text(hud, "BloomPercent", "", bold, 64, new Color32(0xE8, 0x93, 0xAB, 255), 330, -80, 260, 90);
        var countPill = Img(hud, "CountPill", pill, Color.white);
        countPill.type = Image.Type.Sliced;
        Place(countPill.rectTransform, 744, 320, 230, 72);
        game.flowerCountText = Text(countPill.transform, "Count", "ดอกไม้ 0", regular, 30, Ink, 0, 0, 230, 72);
        var pauseBtn = Button(hud, "PauseButton", pill, Color.white, 744, 424, 200, 72, "พัก", Ink, 30);
        game.pauseButtonText = pauseBtn.GetComponentInChildren<TextMeshProUGUI>();
        UnityEventTools.AddVoidPersistentListener(pauseBtn.onClick, game.TogglePause);

        // ป้ายหายใจเข้า/ออก (element ใช้ซ้ำ)
        if (!File.Exists(BreathPrefab)) BreezStatusElementsBuilder.CreatePrefabs(false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BreathPrefab);
        if (prefab != null)
        {
            var bs = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hud);
            Place((RectTransform)bs.transform, 0, 305, 458, 90);
            var ui = bs.GetComponent<BreathStatusUI>();
            if (ui != null) { ui.threshold = threshold; PrefabUtility.RecordPrefabInstancePropertyModifications(ui); }
        }

        BuildPause(C, game);
        BuildSummary(C, game);

        foreach (var r in scene.GetRootGameObjects()) BreezThaiFontInstaller.Apply(r);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        bool hooked = HookBreathPlayButton();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EditorUtility.DisplayDialog("Breez",
            "สร้าง CalmFlow.unity แล้ว\n\n• เลือกเวลา 1/2/3 นาที แล้วกด Start Session\n• เป่า (หรือกดลูกศรขึ้น) ให้ดอกไม้บาน หยุดเป่า = ปลูกลงสวน\n• ปรับ threshold / ช่วงผ่อนผัน / เวลาบานเต็มที่ ที่ GameManager > CalmFlowGame" +
            (hooked ? "\n• ปุ่ม Calm Flow ในหน้า BreathPlay เข้าเกมนี้แล้ว" : "\n• ไม่พบปุ่ม CalmFLow ในหน้า BreathPlay ให้ตั้งปุ่มเองเป็น LoadSceneByName(\"CalmFlow\")"), "OK");
    }

    // ---------------------------------------------------------------
    static BloomFlower BuildFlower(Transform parent)
    {
        var root = UI("Flower", parent);
        Place(root, 0, -360, 10, 10, 0.5f, 0.5f, 0.5f, 0f);
        var f = root.gameObject.AddComponent<BloomFlower>();

        var stem = Img(root, "Stem", pill, Stem);
        stem.type = Image.Type.Sliced;
        Place(stem.rectTransform, 0, 0, 16, 70, 0.5f, 0.5f, 0.5f, 0f);
        var leafL = Img(root, "LeafLeft", leaf, Leaf);
        Place(leafL.rectTransform, 0, 0, 80, 150, 0.5f, 0.5f, 0.5f, 0f);
        var leafR = Img(root, "LeafRight", leaf, Leaf);
        Place(leafR.rectTransform, 0, 0, 80, 150, 0.5f, 0.5f, 0.5f, 0f);

        var head = UI("Head", root);
        Place(head, 0, 70, 10, 10, 0.5f, 0.5f, 0.5f, 0.5f);
        var g = Img(head, "Glow", glow, new Color(1f, 0.93f, 0.95f, 0f));
        Place(g.rectTransform, 0, 0, 560, 560);

        var outer = new List<RectTransform>();
        for (int i = 0; i < 8; i++)
        {
            var p = Img(head, "OuterPetal" + i, petal, f.budColor);
            Place(p.rectTransform, 0, 0, 120, 190, 0.5f, 0.5f, 0.5f, 0.06f);
            outer.Add(p.rectTransform);
        }
        var inner = new List<RectTransform>();
        for (int i = 0; i < 6; i++)
        {
            var p = Img(head, "InnerPetal" + i, petal, f.budColor);
            Place(p.rectTransform, 0, 0, 90, 140, 0.5f, 0.5f, 0.5f, 0.06f);
            inner.Add(p.rectTransform);
        }
        var center = Img(head, "Center", circle, f.centerColor);
        Place(center.rectTransform, 0, 0, 78, 78);

        f.stem = stem.rectTransform;
        f.head = head;
        f.leafLeft = leafL.rectTransform;
        f.leafRight = leafR.rectTransform;
        f.outerPetals = outer.ToArray();
        f.innerPetals = inner.ToArray();
        f.center = center.rectTransform;
        f.glow = g;
        f.stemMax = 300f;
        return f;
    }

    static void BuildSelect(Transform C, CalmFlowGame game)
    {
        var panel = Img(C, "SelectPanel", card, Color.white);
        panel.type = Image.Type.Sliced;
        Place(panel.rectTransform, 0, 130, 1000, 560);
        AddShadow(panel.gameObject);
        var P = panel.transform;
        game.selectPanel = panel.gameObject;

        Text(P, "Title", "Calm Flow", bold, 56, Ink, 0, 215, 800, 70);
        Text(P, "Subtitle", "เลือกระยะเวลาฝึก", regular, 28, Grey, 0, 160, 800, 44);

        string[] labels = { "1 นาที", "2 นาที", "3 นาที" };
        var buttons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            buttons[i] = Button(P, "Duration" + (i + 1), pill, Color.white, -250 + i * 250, 70, 220, 84, labels[i], Grey, 30);
            var o = buttons[i].gameObject.AddComponent<Outline>();
            o.effectColor = new Color32(0xE4, 0xE4, 0xE4, 255);
            o.effectDistance = new Vector2(2, -2);
            UnityEventTools.AddIntPersistentListener(buttons[i].onClick, game.SelectDuration, i);
        }
        game.durationButtons = buttons;

        var ready = Text(P, "Ready", "READY TO BLOOM", bold, 26, Sage, 0, -22, 600, 40);
        ready.characterSpacing = 12;
        game.readyTimerText = Text(P, "ReadyTimer", "02:00", bold, 88, Ink, 0, -98, 500, 100);

        var start = Button(P, "StartButton", pill, Pink, 0, -212, 320, 90, "เริ่มฝึก", Color.white, 34, bold);
        AddShadow(start.gameObject);
        UnityEventTools.AddVoidPersistentListener(start.onClick, game.StartSession);
    }

    static void BuildPause(Transform C, CalmFlowGame game)
    {
        var dim = Img(C, "PausePanel", null, new Color(0.15f, 0.18f, 0.12f, 0.35f));
        Stretch(dim.rectTransform);
        game.pausePanel = dim.gameObject;
        var box = Img(dim.transform, "Card", card, Color.white);
        box.type = Image.Type.Sliced;
        Place(box.rectTransform, 0, 0, 640, 420);
        Text(box.transform, "Title", "พักสักครู่", bold, 52, Ink, 0, 110, 560, 70);
        Text(box.transform, "Body", "หายใจตามสบาย พร้อมแล้วค่อยเล่นต่อ", regular, 28, Grey, 0, 40, 560, 44);
        var resume = Button(box.transform, "ResumeButton", pill, Pink, -135, -100, 240, 90, "เล่นต่อ", Color.white, 32, bold);
        UnityEventTools.AddVoidPersistentListener(resume.onClick, game.Resume);
        var end = Button(box.transform, "EndButton", pill, Green, 135, -100, 240, 90, "จบการฝึก", Color.white, 32, bold);
        UnityEventTools.AddVoidPersistentListener(end.onClick, game.FinishNow);
        dim.gameObject.SetActive(false);
    }

    static void BuildSummary(Transform C, CalmFlowGame game)
    {
        var dim = Img(C, "SummaryPanel", null, new Color(0.15f, 0.18f, 0.12f, 0.3f));
        Stretch(dim.rectTransform);
        game.summaryPanel = dim.gameObject;
        var box = Img(dim.transform, "Card", card, Color.white);
        box.type = Image.Type.Sliced;
        Place(box.rectTransform, 0, 0, 1240, 730);
        var B = box.transform;

        var header = Img(B, "Header", pill, PinkSoft);
        header.type = Image.Type.Sliced;
        Place(header.rectTransform, 0, 280, 620, 100);
        Text(header.transform, "Text", "Your Garden Has Grown!", bold, 42, Ink, 0, 0, 620, 100);
        game.summarySubtitle = Text(B, "Subtitle", "ฝึกครบ 2 นาทีแล้ว", regular, 30, Grey, 0, 190, 900, 46);

        string[] labels = { "ดอกไม้ที่ปลูก", "ดอกใหญ่ที่สุด", "หายใจออกนานสุด", "เวลาหายใจออกรวม" };
        string[] samples = { "0", "0%", "0.0 วิ", "0 วิ" };
        var values = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            var tile = Img(B, "Stat" + i, card, Tile);
            tile.type = Image.Type.Sliced;
            Place(tile.rectTransform, -432 + i * 288, 5, 260, 230);
            values[i] = Text(tile.transform, "Value", samples[i], bold, 70, Ink, 0, 28, 250, 100);
            Text(tile.transform, "Label", labels[i], regular, 24, Grey, 0, -62, 250, 40);
        }
        game.flowersGrownText = values[0];
        game.largestFlowerText = values[1];
        game.longestExhaleText = values[2];
        game.totalExhaleText = values[3];

        var replay = Button(B, "ReplayButton", pill, Pink, -140, -245, 250, 96, "เล่นอีกครั้ง", Color.white, 34, bold);
        AddShadow(replay.gameObject);
        var loader = replay.gameObject.AddComponent<SceneLoader>();
        UnityEventTools.AddVoidPersistentListener(replay.onClick, loader.ReloadCurrent);
        var home = Button(B, "HomeButton", pill, Green, 140, -245, 250, 96, "หน้าหลัก", Color.white, 34, bold);
        AddShadow(home.gameObject);
        LoadScene(home, SceneInBuild("Mode") ? "Mode" : "BreathPlay");   // หน้าหลัก = หน้าเลือกโหมด
        dim.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------
    static bool HookBreathPlayButton()
    {
        if (!File.Exists(BreathPlayPath)) return false;
        var scene = EditorSceneManager.OpenScene(BreathPlayPath, OpenSceneMode.Single);
        Button target = null;
        foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (b.gameObject.scene != scene) continue;
            string n = b.name.Replace(" ", "").ToLowerInvariant();
            if (n == "calmflow") { target = b; break; }
        }
        if (target == null) return false;
        LoadScene(target, SceneName);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static void LoadScene(Button b, string sceneName)
    {
        var loader = b.GetComponent<SceneLoader>();
        if (loader == null) loader = b.gameObject.AddComponent<SceneLoader>();
        while (b.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(b.onClick, 0);
        UnityEventTools.AddStringPersistentListener(b.onClick, loader.LoadSceneByName, sceneName);
        EditorUtility.SetDirty(b);
    }

    static bool SceneInBuild(string name)
    {
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled && Path.GetFileNameWithoutExtension(s.path) == name) return true;
        return false;
    }

    static void AddToBuildSettings(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in list) if (s.path == path) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ---------------------------------------------------------------
    static RectTransform UI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image Img(Transform parent, string name, Sprite sprite, Color color)
    {
        var rt = UI(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = sprite == null; // ฉากมืดกันกดทะลุ
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

    static Button Button(Transform parent, string name, Sprite sprite, Color color, float x, float y, float w, float h,
                         string label, Color textColor, float size, TMP_FontAsset font = null)
    {
        var img = Img(parent, name, sprite, color);
        img.type = sprite == pill || sprite == card ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = true;
        Place(img.rectTransform, x, y, w, h);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors; cb.highlightedColor = new Color(0.96f, 0.96f, 0.96f); cb.pressedColor = new Color(0.85f, 0.85f, 0.85f); b.colors = cb;
        if (!string.IsNullOrEmpty(label))
        {
            var t = Text(img.transform, "Label", label, font != null ? font : regular, size, textColor, 0, 0, w, h);
            Stretch(t.rectTransform);
        }
        return b;
    }

    static void AddShadow(GameObject go)
    {
        var s = go.AddComponent<Shadow>();
        s.effectColor = new Color(0, 0, 0, 0.10f);
        s.effectDistance = new Vector2(0, -4);
    }

    static void Place(RectTransform rt, float x, float y, float w, float h,
                      float ax = 0.5f, float ay = 0.5f, float px = 0.5f, float py = 0.5f)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(px, py);
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
