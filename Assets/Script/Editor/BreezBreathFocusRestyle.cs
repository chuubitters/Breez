using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// Breez > Restyle Breathe & Focus  (กดซ้ำได้)
//   • หน้า "วิธีการเล่น": การ์ดขาว + ภาพตัวละครกับก้อนเมฆ + คำอธิบาย + ปุ่ม "เริ่ม"
//   • หน้า "ผลลัพธ์": คะแนน / ระยะเวลาที่ใช้ (นาที) / ข้ามสิ่งกีดขวางได้ (ชิ้น) + Replay / Home
//   • ปุ่ม Back มุมซ้ายบน (กลับหน้า BreathPlay)
//   ของเดิม (StartPanel / GameOverPanel) ถูกซ่อนไว้ ไม่ลบ
// ตำแหน่งอ้างอิงจอ 1920x1080 (จุดกึ่งกลาง = 0,0)
// =====================================================================
public static class BreezBreathFocusRestyle
{
    const string ScenePath = "Assets/Scenes/BreathFocus.unity";
    const string CloudArt = "Assets/element/B&F/clound.png";

    static readonly Color Ink = new Color32(34, 34, 34, 255);
    static readonly Color Grey = new Color32(70, 70, 70, 255);
    static readonly Color HeaderPink = new Color32(252, 227, 232, 255);
    static readonly Color Pink = new Color32(242, 189, 201, 255);
    static readonly Color Green = new Color32(212, 229, 157, 255);
    static readonly Color CloudTint = new Color32(0xE3, 0xED, 0xF1, 255);
    static readonly Color Silhouette = new Color32(0x55, 0x55, 0x55, 255);

    static TMP_FontAsset regular, bold;
    static Sprite pill, card, arrow;

    [MenuItem("Breez/Restyle Breathe & Focus")]
    public static void Restyle()
    {
        if (!File.Exists(ScenePath)) { EditorUtility.DisplayDialog("Breez", "ไม่พบ " + ScenePath, "OK"); return; }
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        var scene = EditorSceneManager.GetActiveScene();
        var gm = Object.FindObjectOfType<BFGameManager>();
        if (gm == null) { EditorUtility.DisplayDialog("Breez", "ไม่พบ BFGameManager", "OK"); return; }

        Canvas canvas = null;
        if (gm.startPanel != null) canvas = gm.startPanel.transform.GetComponentInParent<Canvas>(true);
        if (canvas == null && gm.gameOverPanel != null) canvas = gm.gameOverPanel.transform.GetComponentInParent<Canvas>(true);
        if (canvas == null) canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { EditorUtility.DisplayDialog("Breez", "ไม่พบ Canvas", "OK"); return; }
        var C = canvas.rootCanvas.transform;
        Undo.RecordObject(gm, "Restyle Breathe & Focus");

        regular = BreezThaiFontInstaller.EnsureFonts();
        bold = BreezThaiFontInstaller.BoldFont;
        if (bold == null) bold = regular;
        pill = BreezStatusElementsBuilder.MakeSprite("ui_pill", BreezSpriteArt.RoundedRect(128, 63), 128, 63);
        card = BreezStatusElementsBuilder.MakeSprite("ui_card", BreezSpriteArt.RoundedRect(128, 40), 128, 40);
        arrow = BreezStatusElementsBuilder.MakeSprite("ui_arrow_left", BreezSpriteArt.ArrowLeft(128), 128, 0);

        var scaler = C.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            Undo.RecordObject(scaler, "Canvas scaler");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // ของเดิม: ซ่อน
        var oldStart = gm.startPanel;
        var oldOver = gm.gameOverPanel;
        if (oldStart != null && oldStart.name != "HowToPlayPanel") oldStart.SetActive(false);
        if (oldOver != null && oldOver.name != "ResultPanel") oldOver.SetActive(false);
        // PlayerController มี gameOverUI ของตัวเอง => ไม่ใช้ (หน้าผลลัพธ์มาจาก BFGameManager)
        foreach (var pc in Object.FindObjectsOfType<PlayerController>()) { Undo.RecordObject(pc, "Restyle"); pc.gameOverUI = null; }

        // ---------- วิธีการเล่น ----------
        var how = Rebuild(C, "HowToPlayPanel");
        var howCard = Img(how, "Card", card, Color.white); howCard.type = Image.Type.Sliced;
        Place(howCard.rectTransform, 0, 10, 1411, 753);
        Shadow(howCard.gameObject, 0.06f);
        var H = howCard.transform;
        Text(H, "Title", "วิธีการเล่น", bold, 54, Ink, 0, 258, 800, 80);

        var player = Object.FindObjectOfType<PlayerController>();
        Sprite playerSprite = null;
        if (player != null) { var sr = player.GetComponentInChildren<SpriteRenderer>(); if (sr != null) playerSprite = sr.sprite; }
        var cloudSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CloudArt);
        var cloudA = Img(H, "CloudUpper", cloudSprite != null ? cloudSprite : card, CloudTint); cloudA.preserveAspect = true;
        Place(cloudA.rectTransform, 90, 115, 205, 132); Shadow(cloudA.gameObject, 0.12f);
        var cloudB = Img(H, "CloudLower", cloudSprite != null ? cloudSprite : card, CloudTint); cloudB.preserveAspect = true;
        Place(cloudB.rectTransform, -205, -40, 205, 132); Shadow(cloudB.gameObject, 0.12f);
        if (playerSprite != null)
        {
            var cat = Img(H, "Player", playerSprite, Silhouette); cat.preserveAspect = true;
            Place(cat.rectTransform, -80, 98, 100, 80);
        }
        Text(H, "Description", "เป่าตัวเครื่องเพื่อให้ตัวละครลอยขึ้นและข้ามสิ่งกีดขวางให้ได้มากที่สุด", regular, 36, Ink, 0, -128, 1200, 60);
        var start = Button(H, "StartButton", Pink, 0, -268, 238, 96, "เริ่ม", 36);
        BackButton(how);

        // ---------- ผลลัพธ์ ----------
        var res = Rebuild(C, "ResultPanel");
        var dim = res.GetComponent<Image>();
        dim.color = new Color(1f, 0.97f, 0.97f, 0.35f);
        var resCard = Img(res, "Card", card, Color.white); resCard.type = Image.Type.Sliced;
        Place(resCard.rectTransform, 0, 20, 1338, 853);
        Shadow(resCard.gameObject, 0.08f);
        var R = resCard.transform;
        var head = Img(R, "Header", pill, HeaderPink); head.type = Image.Type.Sliced;
        Place(head.rectTransform, 0, 312, 266, 92);
        Text(head.transform, "Text", "ผลลัพธ์", bold, 38, Ink, 0, 0, 266, 92);
        Text(R, "ScoreLabel", "คะแนน", regular, 34, Ink, 0, 205, 400, 50);
        gm.resultScoreText = Text(R, "Score", "0", regular, 130, Ink, 0, 102, 700, 150);
        Left(Text(R, "TimeLabel", "ระยะเวลาที่ใช้", regular, 32, Ink, 0, -24, 420, 46), -367);
        gm.resultTimeText = Left(Text(R, "Time", "00:00<size=40%>  นาที</size>", regular, 84, Ink, 0, -102, 440, 100), -367);
        Left(Text(R, "ObstacleLabel", "คุณข้ามสิ่งกีดขวางได้ทั้งหมด", regular, 32, Ink, 0, -24, 520, 46), 75);
        gm.resultObstacleText = Left(Text(R, "Obstacles", "0<size=40%>  ชิ้น</size>", regular, 84, Ink, 0, -102, 440, 100), 75);

        var replay = Button(R, "ReplayButton", Pink, -134, -274, 238, 96, "Replay", 36);
        var loader = replay.gameObject.AddComponent<SceneLoader>();
        UnityEventTools.AddVoidPersistentListener(replay.onClick, loader.ReloadCurrent);
        var home = Button(R, "HomeButton", Green, 134, -274, 238, 96, "Home", 36);
        LoadScene(home, SceneInBuild("Mode") ? "Mode" : "BreathPlay");   // หน้าหลัก = หน้าเลือกโหมด
        BackButton(res);
        res.gameObject.SetActive(false);

        // ---------- ต่อสาย ----------
        gm.startPanel = how.gameObject;
        gm.startButton = start;
        gm.gameOverPanel = res.gameObject;
        how.gameObject.SetActive(true);

        // ตัวนับถอยหลังอยู่บนสุด
        if (gm.countdownText != null) gm.countdownText.transform.SetAsLastSibling();

        BreezThaiFontInstaller.Apply(C.gameObject);
        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Breez", "จัดหน้า Breathe & Focus แล้ว\n\n• หน้าวิธีการเล่น + ปุ่มเริ่ม\n• หน้าผลลัพธ์ใหม่ + Replay / Home\n\nกด Play เพื่อทดสอบ", "OK");
    }

    // ---------------------------------------------------------------
    static RectTransform Rebuild(Transform C, string name)
    {
        var old = C.Find(name);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var img = Img(C, name, null, new Color(1, 1, 1, 0f));
        img.raycastTarget = true;   // กันกดทะลุ
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();
        return rt;
    }

    static void BackButton(Transform panel)
    {
        var go = UI("BackButton", panel);
        var rt = (RectTransform)go;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(190, 70);
        rt.anchoredPosition = new Vector2(84, -64);
        var hit = go.gameObject.AddComponent<Image>();
        hit.color = new Color(1, 1, 1, 0f);
        var b = go.gameObject.AddComponent<Button>();
        b.targetGraphic = hit;
        var a = Img(go, "Arrow", arrow, Ink);
        var art = a.rectTransform;
        art.anchorMin = art.anchorMax = new Vector2(0f, 0.5f); art.pivot = new Vector2(0f, 0.5f);
        art.sizeDelta = new Vector2(34, 30); art.anchoredPosition = new Vector2(10, 0);
        var t = Text(go, "Label", "Back", regular, 38, Ink, 0, 0, 130, 60);
        var trt = t.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f); trt.pivot = new Vector2(0f, 0.5f);
        trt.anchoredPosition = new Vector2(56, 2);
        t.alignment = TextAlignmentOptions.Left;
        LoadScene(b, "BreathPlay");
    }

    static Button Button(Transform parent, string name, Color color, float x, float y, float w, float h, string label, float size)
    {
        var img = Img(parent, name, pill, color);
        img.type = Image.Type.Sliced;
        img.raycastTarget = true;
        Place(img.rectTransform, x, y, w, h);
        Shadow(img.gameObject, 0.10f);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors; cb.highlightedColor = new Color(0.96f, 0.96f, 0.96f); cb.pressedColor = new Color(0.85f, 0.85f, 0.85f); b.colors = cb;
        Text(img.transform, "Label", label, regular, size, Color.white, 0, 0, w, h);
        return b;
    }

    static void LoadScene(Button b, string sceneName)
    {
        var loader = b.GetComponent<SceneLoader>();
        if (loader == null) loader = b.gameObject.AddComponent<SceneLoader>();
        while (b.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(b.onClick, 0);
        UnityEventTools.AddStringPersistentListener(b.onClick, loader.LoadSceneByName, sceneName);
    }

    static bool SceneInBuild(string name)
    {
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled && Path.GetFileNameWithoutExtension(s.path) == name) return true;
        return false;
    }

    static Transform UI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        return go.transform;
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
        var rt = (RectTransform)UI(name, parent);
        Place(rt, x, y, w, h);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    /// <summary>ชิดซ้าย โดย left = ขอบซ้าย (พิกัดกลางจอ)</summary>
    static TextMeshProUGUI Left(TextMeshProUGUI t, float left)
    {
        var rt = t.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(left, rt.anchoredPosition.y);
        t.alignment = TextAlignmentOptions.BottomLeft;
        return t;
    }

    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    static void Shadow(GameObject go, float alpha)
    {
        var s = go.AddComponent<Shadow>();
        s.effectColor = new Color(0, 0, 0, alpha);
        s.effectDistance = new Vector2(0, -6);
    }
}
