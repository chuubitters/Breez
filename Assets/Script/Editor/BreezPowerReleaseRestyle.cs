using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// Breez > Restyle Power Release
// จัดหน้า PowerRelease ให้ตรงดีไซน์ (กดซ้ำได้)
//   • BalloonLayer: ลูกโป่งอยู่เหนือ bg/เมฆ แต่อยู่ใต้ Time / Progress / ป้ายสถานะ
//   • ปุ่ม "← กลับ" (ทับรูปใน bg) กดแล้วไปหน้า BreathPlay
//   • เวลา 02:00 (mm:ss), "จำนวนลูกโป่ง : 0/20", ป้ายหายใจเข้า/ออกด้านล่าง
//   • หน้าผลลัพธ์: ผลลัพธ์ / คะแนน / ระยะเวลาที่ใช้ (นาที) / ลูกโป่งที่เป่าได้ / ปุ่มเล่นอีกครั้ง + หน้าหลัก
// ตำแหน่งทั้งหมดอ้างอิงจอ 1920x1080 (จุดกึ่งกลางจอ = 0,0)
// =====================================================================
public static class BreezPowerReleaseRestyle
{
    const string ScenePath = "Assets/Scenes/PowerRelease.unity";
    const string BreathPrefab = "Assets/Prefabs/Breez/BreathStatus.prefab";
    const string BackScene = "BreathPlay";
    const string HomeScene = "Home";

    static readonly Color Ink = new Color32(51, 51, 51, 255);
    static readonly Color Grey = new Color32(112, 112, 112, 255);
    static readonly Color HeaderPink = new Color32(252, 227, 232, 255);
    static readonly Color ReplayPink = new Color32(242, 189, 201, 255);
    static readonly Color HomeGreen = new Color32(212, 229, 157, 255);

    static TMP_FontAsset thai, bold;
    static Sprite pill;

    [MenuItem("Breez/Restyle Power Release")]
    public static void Restyle()
    {
        if (!File.Exists(ScenePath)) { EditorUtility.DisplayDialog("Breez", "ไม่พบ " + ScenePath, "OK"); return; }
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        var scene = EditorSceneManager.GetActiveScene();

        var gm = Object.FindObjectOfType<PWGameManager>();
        if (gm == null) { EditorUtility.DisplayDialog("Breez", "ไม่พบ PWGameManager ใน scene", "OK"); return; }
        Transform canvas = gm.canvasTransform;
        if (canvas == null)
        {
            var c = Object.FindObjectOfType<Canvas>();
            if (c == null) { EditorUtility.DisplayDialog("Breez", "ไม่พบ Canvas", "OK"); return; }
            canvas = c.transform;
        }
        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Restyle Power Release");
        Undo.RecordObject(gm, "Restyle Power Release");

        thai = BreezThaiFontInstaller.EnsureFonts();
        bold = BreezThaiFontInstaller.BoldFont;
        if (bold == null) bold = thai;
        pill = BreezStatusElementsBuilder.MakeSprite("ui_pill", BreezSpriteArt.RoundedRect(128, 63), 128, 63);

        // ---------- Canvas: สเกลตามจอ (ดีไซน์ 1920x1080) ----------
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            Undo.RecordObject(scaler, "Canvas scaler");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // ---------- Balloon layer ----------
        var layer = canvas.Find("BalloonLayer") as RectTransform;
        if (layer == null)
        {
            layer = (RectTransform)new GameObject("BalloonLayer", typeof(RectTransform)).transform;
            Undo.RegisterCreatedObjectUndo(layer.gameObject, "BalloonLayer");
            layer.SetParent(canvas, false);
        }
        layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one;
        layer.offsetMin = layer.offsetMax = Vector2.zero;
        layer.SetSiblingIndex(BackgroundCount(canvas, layer));
        gm.balloonLayer = layer;

        // ---------- ปุ่มกลับ (โปร่งใส ทับรูป "← กลับ" ที่อยู่ใน bg) ----------
        var back = FindOrCreate(canvas, "BackButton", 242, 72);
        Place(back, -744, 424, 242, 72);
        var backImg = Get<Image>(back.gameObject);
        backImg.sprite = pill; backImg.type = Image.Type.Sliced;
        backImg.color = new Color(1, 1, 1, 0f);
        backImg.raycastTarget = true;
        var backBtn = Get<Button>(back.gameObject);
        backBtn.transition = Selectable.Transition.None;
        SetLoadScene(backBtn, BackScene);

        // ---------- เวลา ----------
        if (gm.timerText != null)
        {
            var timerPill = gm.timerText.transform.parent as RectTransform;
            if (timerPill != null && timerPill != canvas && timerPill.GetComponent<Image>() != null)
            {
                Place(timerPill, 0, 413, 234, 91);
                Stretch((RectTransform)gm.timerText.transform);
            }
            else Place((RectTransform)gm.timerText.transform, 0, 413, 234, 91);
            Style(gm.timerText, bold, 44, Ink, "02:00");
            gm.timerText.characterSpacing = 2;
        }

        // ---------- จำนวนลูกโป่ง ----------
        if (gm.progressText != null)
        {
            Place((RectTransform)gm.progressText.transform, 0, 309, 900, 64);
            Style(gm.progressText, thai, 38, Ink, "จำนวนลูกโป่ง : 0/" + gm.totalBalloons);
        }
        gm.progressFormat = "จำนวนลูกโป่ง : {0}/{1}";

        // ---------- ป้ายหายใจเข้า/ออก ----------
        RectTransform breath = null;
        foreach (Transform t in canvas) if (t.name == "BreathStatus") breath = (RectTransform)t;
        if (breath == null)
        {
            if (!File.Exists(BreathPrefab)) BreezStatusElementsBuilder.CreatePrefabs(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BreathPrefab);
            if (prefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
                Undo.RegisterCreatedObjectUndo(go, "BreathStatus");
                breath = (RectTransform)go.transform;
            }
        }
        if (breath != null)
        {
            Place(breath, 0, -389, 458, 90);
            var bs = breath.GetComponent<BreathStatusUI>();
            if (bs != null) { bs.threshold = gm.breathThreshold; PrefabUtility.RecordPrefabInstancePropertyModifications(bs); }
            PrefabUtility.RecordPrefabInstancePropertyModifications(breath);
        }

        // ---------- หน้าผลลัพธ์ ----------
        if (gm.gameOverPanel != null) RestyleResult(gm);

        // HUD อยู่เหนือลูกโป่ง, หน้าผลลัพธ์บนสุด
        if (gm.timerText != null)
        {
            var hud = gm.timerText.transform.parent == canvas ? gm.timerText.transform : gm.timerText.transform.parent;
            hud.SetSiblingIndex(layer.GetSiblingIndex() + 1);
        }
        if (gm.progressText != null && gm.progressText.transform.parent == canvas)
            gm.progressText.transform.SetSiblingIndex(layer.GetSiblingIndex() + 1);
        back.SetAsLastSibling();
        if (breath != null) breath.SetAsLastSibling();
        if (gm.gameOverPanel != null && gm.gameOverPanel.transform.parent == canvas) gm.gameOverPanel.transform.SetAsLastSibling();

        BreezThaiFontInstaller.Apply(canvas.gameObject, thai);

        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Breez", "จัดหน้า PowerRelease ตามดีไซน์แล้ว\n\n• ลูกโป่งอยู่ใน BalloonLayer (ใต้ข้อความ)\n• หน้าผลลัพธ์ใหม่ + ปุ่มเล่นอีกครั้ง / หน้าหลัก\n\nกด Play เพื่อทดสอบ", "OK");
    }

    // ---------------------------------------------------------------
    static void RestyleResult(PWGameManager gm)
    {
        var panel = gm.gameOverPanel.transform;

        // การ์ดขาว (rs-panel) = Image ลูกตัวแรกของ Panel
        RectTransform card = null;
        foreach (Transform t in panel)
            if (t.GetComponent<Image>() != null) { card = (RectTransform)t; break; }
        if (card == null) card = (RectTransform)panel;

        // หัวข้อ "ผลลัพธ์" (เดิมเป็นป้ายไล่สี rs.png)
        RectTransform header = null;
        foreach (Transform t in card)
        {
            var img = t.GetComponent<Image>();
            if (img != null && t.GetComponent<Button>() == null && t.GetComponentInChildren<TMP_Text>(true) != null) { header = (RectTransform)t; break; }
        }
        if (header == null) header = FindOrCreate(card, "Header", 267, 90);
        Place(header, 0, 311, 267, 90);
        var hImg = Get<Image>(header.gameObject);
        hImg.sprite = pill; hImg.type = Image.Type.Sliced; hImg.color = HeaderPink; hImg.preserveAspect = false;
        var hText = header.GetComponentInChildren<TextMeshProUGUI>(true);
        if (hText == null) hText = NewText(header, "Text (TMP)");
        Stretch((RectTransform)hText.transform);
        Style(hText, bold, 40, Ink, "ผลลัพธ์");

        // ข้อความเดิม MISSION COMPLETE! / TIME'S UP! ไม่มีในดีไซน์ => ซ่อน
        if (gm.resultText != null) gm.resultText.gameObject.SetActive(false);

        // คะแนน
        Label(card, "ScoreLabel", "คะแนน", 0, 190, 400, 50, 30, Grey);
        if (gm.finalScoreText != null)
        {
            Place((RectTransform)gm.finalScoreText.transform, 0, 84, 700, 170);
            Style(gm.finalScoreText, bold, 140, Ink, "0");
        }

        // ระยะเวลาที่ใช้
        Label(card, "TimeLabel", "ระยะเวลาที่ใช้", -117, -51, 360, 44, 26, Grey);
        if (gm.timeTakenText != null)
        {
            Place((RectTransform)gm.timeTakenText.transform, -133, -121, 300, 104);
            Style(gm.timeTakenText, bold, 80, Ink, "00:00");
        }
        Label(card, "TimeUnit", "นาที", 22, -138, 90, 40, 24, Grey).alignment = TextAlignmentOptions.Left;

        // ลูกโป่งที่เป่าได้
        Label(card, "BalloonsLabel", "ลูกโป่งที่เป่าได้", 208, -51, 360, 44, 26, Grey);
        var value = card.Find("BalloonsValue");
        var bv = value != null ? value.GetComponent<TextMeshProUGUI>() : NewText(card, "BalloonsValue");
        Place((RectTransform)bv.transform, 228, -121, 320, 104);
        Style(bv, bold, 80, Ink, "0/" + gm.totalBalloons);
        gm.balloonsResultText = bv;

        // ปุ่ม: หาจากรูปเดิม rp-bt (เล่นใหม่) / hom-bt (หน้าหลัก)
        Button replay = null, home = null;
        foreach (var b in card.GetComponentsInChildren<Button>(true))
        {
            var img = b.GetComponent<Image>();
            string sprite = img != null && img.sprite != null ? img.sprite.name : "";
            if (b.name == "ReplayButton" || sprite.StartsWith("rp")) replay = b;
            else if (b.name == "HomeButton" || sprite.StartsWith("hom")) home = b;
        }
        replay = PillButton(card, replay, "ReplayButton", "เล่นอีกครั้ง", ReplayPink, -133, -274);
        home = PillButton(card, home, "HomeButton", "หน้าหลัก", HomeGreen, 132, -274);

        var loader = Get<SceneLoader>(replay.gameObject);
        ClearListeners(replay);
        UnityEventTools.AddVoidPersistentListener(replay.onClick, loader.ReloadCurrent);
        SetLoadScene(home, SceneInBuild(HomeScene) ? HomeScene : BackScene);
    }

    static Button PillButton(Transform card, Button b, string name, string text, Color color, float x, float y)
    {
        RectTransform rt;
        if (b == null) { rt = FindOrCreate(card, name, 238, 96); b = Get<Button>(rt.gameObject); }
        else rt = (RectTransform)b.transform;
        rt.name = name;
        Place(rt, x, y, 238, 96);

        var img = Get<Image>(rt.gameObject);
        img.sprite = pill; img.type = Image.Type.Sliced; img.color = color; img.preserveAspect = false;
        b.targetGraphic = img;
        b.transition = Selectable.Transition.ColorTint;
        var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(0.96f, 0.96f, 0.96f); cb.pressedColor = new Color(0.85f, 0.85f, 0.85f); b.colors = cb;
        if (rt.GetComponent<Shadow>() == null)
        {
            var s = Undo.AddComponent<Shadow>(rt.gameObject);
            s.effectColor = new Color(0, 0, 0, 0.10f);
            s.effectDistance = new Vector2(0, -4);
        }

        var label = rt.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null) label = NewText(rt, "Label");
        label.gameObject.SetActive(true);
        Stretch((RectTransform)label.transform);
        Style(label, bold, 34, Color.white, text);
        return b;
    }

    // ---------------------------------------------------------------
    static int BackgroundCount(Transform canvas, Transform skip)
    {
        // bg + เมฆ = Image ที่ไม่มีลูกและไม่ใช่ปุ่ม ซึ่งอยู่ก่อน UI อื่น
        int index = 0;
        for (int i = 0; i < canvas.childCount; i++)
        {
            var c = canvas.GetChild(i);
            if (c == skip) continue;
            if (c.GetComponent<Image>() != null && c.childCount == 0 && c.GetComponent<Selectable>() == null) index++;
            else break;
        }
        return index;
    }

    static TextMeshProUGUI Label(Transform parent, string name, string text, float x, float y, float w, float h, float size, Color color)
    {
        var t = parent.Find(name);
        var tmp = t != null ? t.GetComponent<TextMeshProUGUI>() : NewText(parent, name);
        Place((RectTransform)tmp.transform, x, y, w, h);
        Style(tmp, thai, size, color, text);
        return tmp;
    }

    static TextMeshProUGUI NewText(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<TextMeshProUGUI>();
    }

    static void Style(TextMeshProUGUI t, TMP_FontAsset font, float size, Color color, string text)
    {
        Undo.RecordObject(t, "Style text");
        if (font != null) t.font = font;
        t.fontSize = size;
        t.enableAutoSizing = false;
        t.fontStyle = FontStyles.Normal;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.text = text;
        EditorUtility.SetDirty(t);
    }

    static RectTransform FindOrCreate(Transform parent, string name, float w, float h)
    {
        var t = parent.Find(name);
        if (t != null) return (RectTransform)t;
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(w, h);
        return (RectTransform)go.transform;
    }

    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        Undo.RecordObject(rt, "Place");
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    static void Stretch(RectTransform rt)
    {
        Undo.RecordObject(rt, "Stretch");
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    static T Get<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = Undo.AddComponent<T>(go);
        else Undo.RecordObject(c, "Edit " + typeof(T).Name);
        return c;
    }

    static void ClearListeners(Button b)
    {
        Undo.RecordObject(b, "Button listeners");
        while (b.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(b.onClick, 0);
    }

    static void SetLoadScene(Button b, string sceneName)
    {
        var loader = Get<SceneLoader>(b.gameObject);
        ClearListeners(b);
        UnityEventTools.AddStringPersistentListener(b.onClick, loader.LoadSceneByName, sceneName);
        EditorUtility.SetDirty(b);
    }

    static bool SceneInBuild(string name)
    {
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled && Path.GetFileNameWithoutExtension(s.path) == name) return true;
        return false;
    }
}
