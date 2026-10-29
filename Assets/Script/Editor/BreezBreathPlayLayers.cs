using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// Breez > Fix Layers + How To Play (Breath Play)
// ใช้กับ 3 เกมใน Breath Play: Power Release / Breathe & Focus / Calm Flow (กดซ้ำได้)
//   • ย้ายหน้าที่ต้องอยู่บนสุด (วิธีการเล่น / ผลลัพธ์ / พัก / นับถอยหลัง) ไปไว้ใน "BreezTopCanvas"
//     (Screen Space Overlay, Sort Order 100) => อยู่บนลูกโป่ง ก้อนเมฆ ตัวละคร และ UI อื่นเสมอ
//   • เพิ่มหน้า "วิธีการเล่น" ให้ Power Release และ Calm Flow (Breathe & Focus มีจาก Restyle แล้ว)
//   • Calm Flow: ลบ UI ของ Power Release ที่ติดมาจากตอนคัดลอก scene
// =====================================================================
public static class BreezBreathPlayLayers
{
    const string PowerPath = "Assets/Scenes/PowerRelease.unity";
    const string FocusPath = "Assets/Scenes/BreathFocus.unity";
    const string CalmPath = "Assets/Scenes/CalmFlow.unity";
    const string TopName = "BreezTopCanvas";

    static readonly Color Ink = new Color32(34, 34, 34, 255);
    static readonly Color Pink = new Color32(242, 189, 201, 255);

    static TMP_FontAsset regular, bold;
    static Sprite pill, card, arrow, flower;

    [MenuItem("Breez/Fix Layers + How To Play (Breath Play)")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        regular = BreezThaiFontInstaller.EnsureFonts();
        bold = BreezThaiFontInstaller.BoldFont;
        if (bold == null) bold = regular;
        pill = BreezStatusElementsBuilder.MakeSprite("ui_pill", BreezSpriteArt.RoundedRect(128, 63), 128, 63);
        card = BreezStatusElementsBuilder.MakeSprite("ui_card", BreezSpriteArt.RoundedRect(128, 40), 128, 40);
        arrow = BreezStatusElementsBuilder.MakeSprite("ui_arrow_left", BreezSpriteArt.ArrowLeft(128), 128, 0);
        flower = BreezStatusElementsBuilder.MakeSprite("ui_flower", BreezSpriteArt.Flower(128), 128, 0);

        string previous = EditorSceneManager.GetActiveScene().path;
        var log = new System.Text.StringBuilder();
        if (File.Exists(PowerPath)) log.AppendLine(Power());
        if (File.Exists(FocusPath)) log.AppendLine(Focus());
        if (File.Exists(CalmPath)) log.AppendLine(Calm());
        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Breez", log + "\nหน้าวิธีการเล่น / ผลลัพธ์ อยู่บนสุดเสมอแล้ว", "OK");
    }

    // ---------------------------------------------------------------
    static string Power()
    {
        EditorSceneManager.OpenScene(PowerPath, OpenSceneMode.Single);
        var gm = Object.FindObjectOfType<PWGameManager>();
        if (gm == null) return "✗ Power Release: ไม่พบ PWGameManager";
        Undo.RecordObject(gm, "Layers");
        var top = TopCanvas();

        var how = HowTo(top, "เป่าแรงๆ ให้ลูกโป่งพองจนแตก\nเป่าให้แตกครบทุกลูกก่อนหมดเวลา", out var start, art =>
        {
            var a = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/element/balloon/balloon-1.png");
            var b = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/element/balloon/balloon-2.png");
            if (a != null) Art(art, "BalloonRed", a, -85, 95, 150, 200);
            if (b != null) Art(art, "BalloonBlue", b, 85, 80, 130, 175);
        });
        gm.howToPanel = how;
        gm.howToStartButton = start;
        how.SetActive(false);   // PWGameManager เปิดเองตอนเริ่ม

        MoveToTop(gm.gameOverPanel, top);
        how.transform.SetAsLastSibling();
        return Save(gm, "Power Release");
    }

    static string Focus()
    {
        EditorSceneManager.OpenScene(FocusPath, OpenSceneMode.Single);
        var gm = Object.FindObjectOfType<BFGameManager>();
        if (gm == null) return "✗ Breathe & Focus: ไม่พบ BFGameManager";
        Undo.RecordObject(gm, "Layers");
        var top = TopCanvas();
        if (gm.startPanel == null || gm.startPanel.name != "HowToPlayPanel")
            return "✗ Breathe & Focus: กด Breez > Restyle Breathe & Focus ก่อน แล้วค่อยกดเมนูนี้อีกครั้ง";
        MoveToTop(gm.gameOverPanel, top);
        MoveToTop(gm.startPanel, top);
        if (gm.countdownText != null) MoveToTop(gm.countdownText.gameObject, top);
        return Save(gm, "Breathe & Focus");
    }

    static string Calm()
    {
        EditorSceneManager.OpenScene(CalmPath, OpenSceneMode.Single);
        var gm = Object.FindObjectOfType<CalmFlowGame>();
        if (gm == null) return "✗ Calm Flow: ไม่พบ CalmFlowGame";
        Undo.RecordObject(gm, "Layers");

        // UI ของ Power Release ที่ติดมากับกล้องตอนคัดลอก scene
        foreach (var c in Object.FindObjectsOfType<Canvas>(true))
        {
            if (c == null || !c.isRootCanvas) continue;
            if (FindDeep(c.transform, "BalloonLayer") != null) Undo.DestroyObjectImmediate(c.gameObject);
        }

        var top = TopCanvas();
        var how = HowTo(top, "ค่อยๆ ผ่อนลมหายใจออกยาวๆ ให้ดอกไม้บาน\nหยุดเป่าเพื่อปลูกดอกไม้ลงสวน แล้วเริ่มดอกใหม่", out var start, art =>
        {
            Art(art, "FlowerLeft", flower, -120, 60, 110, 110).color = new Color32(0xD5, 0xE7, 0x9A, 255);
            Art(art, "FlowerMain", flower, 10, 100, 170, 170).color = new Color32(0xF3, 0xBC, 0xCB, 255);
            Art(art, "FlowerRight", flower, 130, 50, 80, 80).color = new Color32(0xF1, 0xDC, 0x86, 255);
        });
        gm.howToPanel = how;
        gm.howToStartButton = start;
        how.SetActive(false);   // CalmFlowGame เปิดเองตอนเริ่ม

        MoveToTop(gm.summaryPanel, top);
        MoveToTop(gm.pausePanel, top);
        how.transform.SetAsLastSibling();
        return Save(gm, "Calm Flow");
    }

    static string Save(Object gm, string name)
    {
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var r in scene.GetRootGameObjects()) BreezThaiFontInstaller.Apply(r);
        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "✓ " + name;
    }

    // ---------------------------------------------------------------
    /// <summary>Canvas แบบ Overlay ลำดับสูงสุด สำหรับหน้าที่ต้องอยู่บนทุกอย่าง</summary>
    public static Transform TopCanvas()
    {
        foreach (var c in Object.FindObjectsOfType<Canvas>(true))
            if (c != null && c.name == TopName && c.isRootCanvas) return c.transform;
        var go = new GameObject(TopName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, TopName);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return go.transform;
    }

    static void MoveToTop(GameObject panel, Transform top)
    {
        if (panel == null) return;
        Undo.RegisterFullObjectHierarchyUndo(panel, "Move to top");
        panel.transform.SetParent(top, false);   // false = คงตำแหน่ง/anchor เดิม (ทั้งสอง canvas อ้างอิง 1920x1080)
        panel.transform.SetAsLastSibling();
    }

    /// <summary>หน้าวิธีการเล่น (การ์ดขาว + ภาพ + คำอธิบาย + ปุ่มเริ่ม + Back) ตามดีไซน์ Breathe & Focus</summary>
    static GameObject HowTo(Transform top, string description, out Button start, System.Action<Transform> art)
    {
        var old = top.Find("HowToPlayPanel");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var panel = Img(top, "HowToPlayPanel", null, new Color(1f, 1f, 1f, 0.35f));
        panel.raycastTarget = true;
        var prt = panel.rectTransform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;

        var c = Img(panel.transform, "Card", card, Color.white); c.type = Image.Type.Sliced;
        Place(c.rectTransform, 0, 10, 1411, 753);
        Shadow(c.gameObject, 0.06f);
        Text(c.transform, "Title", "วิธีการเล่น", bold, 54, Ink, 0, 258, 800, 80);
        var artRoot = UI("Art", c.transform); Place((RectTransform)artRoot, 0, 0, 10, 10);
        art(artRoot);
        var d = Text(c.transform, "Description", description, regular, 36, Ink, 0, -128, 1250, 120);
        d.lineSpacing = 12;

        var b = Img(c.transform, "StartButton", pill, Pink); b.type = Image.Type.Sliced; b.raycastTarget = true;
        Place(b.rectTransform, 0, -280, 238, 96);
        Shadow(b.gameObject, 0.10f);
        start = b.gameObject.AddComponent<Button>();
        start.targetGraphic = b;
        Text(b.transform, "Label", "เริ่ม", regular, 36, Color.white, 0, 0, 238, 96);

        // Back มุมซ้ายบน
        var back = UI("BackButton", panel.transform);
        var brt = (RectTransform)back;
        brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f); brt.pivot = new Vector2(0f, 1f);
        brt.sizeDelta = new Vector2(190, 70); brt.anchoredPosition = new Vector2(84, -64);
        var hit = back.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, 0f);
        var bb = back.gameObject.AddComponent<Button>(); bb.targetGraphic = hit;
        var a = Img(back, "Arrow", arrow, Ink);
        var art2 = a.rectTransform; art2.anchorMin = art2.anchorMax = new Vector2(0f, 0.5f); art2.pivot = new Vector2(0f, 0.5f);
        art2.sizeDelta = new Vector2(34, 30); art2.anchoredPosition = new Vector2(10, 0);
        var t = Text(back, "Label", "Back", regular, 38, Ink, 0, 0, 130, 60);
        var trt = t.rectTransform; trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f); trt.pivot = new Vector2(0f, 0.5f);
        trt.anchoredPosition = new Vector2(56, 2); t.alignment = TextAlignmentOptions.Left;
        var loader = back.gameObject.AddComponent<SceneLoader>();
        UnityEventTools.AddStringPersistentListener(bb.onClick, loader.LoadSceneByName, "BreathPlay");

        panel.transform.SetAsLastSibling();
        return panel.gameObject;
    }

    static Image Art(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
    {
        var img = Img(parent, name, sprite, Color.white);
        img.preserveAspect = true;
        Place(img.rectTransform, x, y, w, h);
        return img;
    }

    // ---------------------------------------------------------------
    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root) { var f = FindDeep(c, name); if (f != null) return f; }
        return null;
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
        t.raycastTarget = false;
        t.text = text;
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
