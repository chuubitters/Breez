using System;
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
using Object = UnityEngine.Object;

// =====================================================================
// เมนู Breez > Create Gadget Setting Scene
// สร้าง Assets/Scenes/GadgetSetting.unity ตามดีไซน์ (ไอคอน, ชื่ออุปกรณ์, สถานะ)
// + เพิ่ม SerialController (คัดลอกพอร์ตจาก scene PowerRelease) เพื่อให้รู้สถานะสด
// + ทำให้รูป GadgetStatus ใน scene Home กดแล้วเปิดหน้านี้
// =====================================================================
public static class BreezGadgetSceneBuilder
{
    const string ScenePath = "Assets/Scenes/GadgetSetting.unity";
    const string SceneName = "GadgetSetting";
    const string HomeScenePath = "Assets/Scenes/Home.unity";
    const string ArtFolder = "Assets/element/Gadget";
    static readonly string[] SerialSourceScenes = { "Assets/Scenes/PowerRelease.unity", "Assets/Scenes/BreathFocus.unity", "Assets/Scenes/Rainy Forest.unity" };

    static readonly Color Bg = new Color32(243, 245, 236, 255);
    static readonly Color Ink = new Color32(30, 30, 30, 255);
    static readonly Color Grey = new Color32(85, 85, 85, 255);
    static readonly Color LabelGrey = new Color32(95, 95, 95, 255);

    static TMP_FontAsset font;
    static bool thai;
    static Sprite rounded, circle, flower, arrow;

    [MenuItem("Breez/Create Gadget Setting Scene")]
    public static void Create()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Breez", "GadgetSetting.unity already exists. Replace it?", "Replace", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // อ่านค่าพอร์ต serial จาก scene เกมเดิม (ก่อนสร้าง scene ใหม่)
        var serialSettings = ReadSerialSettings();

        // ---------- รูป + font ----------
        var icons = new Dictionary<string, Sprite>();
        foreach (var id in BreezSpriteArt.GadgetIcons)
            icons[id] = MakeSprite($"icon_{id}", BreezSpriteArt.GadgetIcon(id, 192), 192, 0);
        rounded = MakeSprite("ui_rounded", BreezSpriteArt.RoundedRect(128, 40), 128, 40);
        circle = MakeSprite("ui_circle", BreezSpriteArt.Circle(256), 256, 0);
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

        var root = canvasGO.transform;
        var bg = Img("Background", root, null, Bg, 0, 0, 1920, 1080);
        Stretch(bg.rectTransform);
        Img("Blob Yellow", root, circle, new Color32(250, 230, 170, 80), 170, 360, 220, 220);
        Img("Blob Green", root, circle, new Color32(214, 230, 170, 110), -120, 420, 520, 520);
        Img("Blob Blue", root, circle, new Color32(186, 218, 232, 90), 1560, 480, 420, 420);
        Img("Blob Yellow 2", root, circle, new Color32(250, 230, 170, 80), 1540, 330, 220, 220);
        Img("Flower", root, flower, new Color32(222, 236, 190, 255), 1532, 802, 148, 148);

        // ปุ่ม Back
        var back = Img("BackButton", root, null, new Color(1, 1, 1, 0), 88, 66, 200, 70);
        var backBtn = back.gameObject.AddComponent<Button>();
        back.raycastTarget = true;
        Img("Arrow", back.transform, arrow, Ink, 6, 20, 36, 30);
        Txt("Label", back.transform, "Back", 40, Ink, FontStyles.Normal, 50, 6, 150, 58);

        // แผงหลัก
        var panelImg = Img("Panel", root, rounded, Color.white, 274, 164, 1373, 752);
        panelImg.type = Image.Type.Sliced;
        panelImg.pixelsPerUnitMultiplier = 0.45f;
        var panel = panelImg.transform;

        var title = Txt("Title", panel, "Setting", 58, Ink, FontStyles.Normal, 0, 84, 1373, 80);
        title.alignment = TextAlignmentOptions.Center;

        var iconLabel = Txt("IconLabel", panel, thai ? "ไอคอน" : "icon", 22, LabelGrey, FontStyles.Normal, 586, 240, 200, 34);
        iconLabel.alignment = TextAlignmentOptions.Center;

        // วงกลมไอคอน (กดเพื่อเปลี่ยน)
        var ring = Img("IconButton", panel, circle, new Color32(158, 158, 158, 255), 615, 280, 142, 142);
        ring.raycastTarget = true;
        var ringBtn = ring.gameObject.AddComponent<Button>();
        ringBtn.targetGraphic = ring;
        var innerRing = Img("Inner", ring.transform, circle, Color.white, 3, 3, 136, 136);
        innerRing.raycastTarget = false;
        var iconImg = Img("Icon", ring.transform, icons["cat"], Grey, 24, 24, 94, 94);
        iconImg.raycastTarget = false;
        var badge = Img("EditBadge", ring.transform, circle, new Color32(166, 203, 69, 255), 104, 104, 34, 34);
        badge.raycastTarget = false;
        var plus = Txt("Plus", badge.transform, "+", 28, Color.white, FontStyles.Bold, 0, -2, 34, 34);
        plus.alignment = TextAlignmentOptions.Center;

        // ชื่ออุปกรณ์ (แก้ไขได้)
        Txt("NameLabel", panel, thai ? "ชื่ออุปกรณ์" : "Device name", 22, LabelGrey, FontStyles.Normal, 410, 474, 300, 34);
        var nameInput = NameField(panel, 402, 506, 320, 66);
        Img("Underline", panel, null, new Color32(226, 228, 220, 255), 410, 572, 300, 2);

        // สถานะ
        Txt("StatusLabel", panel, thai ? "สถานะ" : "Status", 22, LabelGrey, FontStyles.Normal, 738, 474, 300, 34);
        var dot = Img("StatusDot", panel, circle, new Color32(170, 170, 170, 255), 740, 532, 16, 16);
        var status = Txt("Status", panel, thai ? "ไม่ได้เชื่อมต่อ" : "Not connected", 40, Grey, FontStyles.Normal, 766, 506, 340, 66);
        status.enableWordWrapping = false;
        var battery = Txt("Battery", panel, "", 18, LabelGrey, FontStyles.Normal, 766, 572, 300, 30);

        // ตัวเลือกไอคอน (ซ่อนไว้จนกดวงกลม)
        var pickerImg = Img("IconPicker", panel, rounded, new Color32(247, 248, 243, 255), 386, 606, 600, 108);
        pickerImg.type = Image.Type.Sliced;
        pickerImg.pixelsPerUnitMultiplier = 1.4f;
        pickerImg.raycastTarget = true;
        var options = new List<GadgetSettingsScreen.IconOption>();
        for (int i = 0; i < BreezSpriteArt.GadgetIcons.Length; i++)
        {
            string id = BreezSpriteArt.GadgetIcons[i];
            var frame = Img($"Option {id}", pickerImg.transform, circle, new Color32(236, 238, 232, 255), 18 + i * 97, 14, 80, 80);
            frame.raycastTarget = true;
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            var inner = Img("Inner", frame.transform, circle, Color.white, 4, 4, 72, 72);
            inner.raycastTarget = false;
            var ic = Img("Icon", frame.transform, icons[id], Grey, 16, 16, 48, 48);
            ic.raycastTarget = false;
            options.Add(new GadgetSettingsScreen.IconOption { id = id, button = btn, frame = frame, sprite = icons[id] });
        }

        var toast = Txt("Toast", panel, "", 18, new Color32(107, 152, 97, 255), FontStyles.Normal, 386, 718, 600, 28);
        toast.alignment = TextAlignmentOptions.Center;

        // ---------- ต่อสาย ----------
        var screen = canvasGO.AddComponent<GadgetSettingsScreen>();
        screen.thaiText = thai;
        screen.backScene = "Home";
        screen.backButton = backBtn;
        screen.iconButton = ringBtn;
        screen.iconImage = iconImg;
        screen.iconPicker = pickerImg.gameObject;
        screen.iconOptions = options.ToArray();
        screen.nameInput = nameInput;
        screen.statusText = status;
        screen.statusDot = dot;
        screen.batteryText = battery;
        screen.toastText = toast;
        canvasGO.AddComponent<AccountActions>().requireLogin = true;

        string serialNote = AddSerial(serialSettings);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);

        string wired = WireHome();
        EditorSceneManager.OpenScene(ScenePath);

        EditorUtility.DisplayDialog("Breez",
            "Created Assets/Scenes/GadgetSetting.unity\n\n" + wired + "\n\n" + serialNote +
            (thai ? "\n\nThai font: " + font.name : "\n\nNo Thai font found, using English text."), "OK");
    }

    // ---------------------------------------------------------------
    // Serial (Ardity) — ไม่อ้างชนิดตรงๆ เผื่อ package ยังไม่ถูกโหลด
    // ---------------------------------------------------------------
    class SerialSettings { public string port; public int baud; }

    static Type FindType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            foreach (var t in SafeTypes(asm))
                if (t.Name == name && typeof(MonoBehaviour).IsAssignableFrom(t)) return t;
        return null;
    }

    static Type[] SafeTypes(System.Reflection.Assembly asm)
    {
        try { return asm.GetTypes(); } catch { return new Type[0]; }
    }

    static SerialSettings ReadSerialSettings()
    {
        var type = FindType("SerialController");
        if (type == null) return null;
        foreach (var path in SerialSourceScenes)
        {
            if (!File.Exists(path)) continue;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var comp = Object.FindObjectOfType(type);
            if (comp == null) continue;
            var so = new SerializedObject(comp);
            var port = so.FindProperty("portName");
            var baud = so.FindProperty("baudRate");
            return new SerialSettings
            {
                port = port != null ? port.stringValue : "",
                baud = baud != null ? baud.intValue : 115200,
            };
        }
        return null;
    }

    static string AddSerial(SerialSettings settings)
    {
        var type = FindType("SerialController");
        var go = new GameObject("Gadget Serial");
        go.AddComponent<BreathReceiver>();
        if (type == null)
            return "Ardity SerialController not found — status will use the server's last known state.";

        go.SetActive(false); // ตั้งค่าก่อนเปิด ไม่ให้เชื่อมพอร์ตผิด
        var comp = go.AddComponent(type);
        var so = new SerializedObject(comp);
        if (settings != null) so.FindProperty("portName")?.SetValue(settings.port);
        var baud = so.FindProperty("baudRate");
        if (baud != null) baud.intValue = 115200;      // ESP32-C3
        var dtr = so.FindProperty("dtrEnable");
        if (dtr != null) dtr.boolValue = true;
        go.AddComponent<SerialPortAutoSelect>();       // หาพอร์ต ESP32 อัตโนมัติ
        var listener = so.FindProperty("messageListener");
        if (listener != null) listener.objectReferenceValue = go;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.SetActive(true);

        return "Added SerialController (115200, port auto-detected at Play).";
    }

    static void SetValue(this SerializedProperty p, string v) { if (p != null) p.stringValue = v; }

    // ---------------------------------------------------------------
    // ทำให้ GadgetStatus ใน Home กดได้
    // ---------------------------------------------------------------
    static string WireHome()
    {
        if (!File.Exists(HomeScenePath)) return "Home scene not found — add a button that loads \"GadgetSetting\".";
        var home = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);

        GameObject target = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<RectTransform>())
            if (t.gameObject.scene == home && t.gameObject.name == "GadgetStatus") { target = t.gameObject; break; }
        var loader = Object.FindObjectOfType<SceneLoader>();
        if (target == null || loader == null)
            return "Could not find GadgetStatus in Home — set a button's OnClick to SceneLoader.LoadSceneByName(\"GadgetSetting\").";

        var btn = target.GetComponent<Button>();
        if (btn == null)
        {
            btn = target.AddComponent<Button>();
            btn.targetGraphic = target.GetComponent<Graphic>();
        }
        var g = target.GetComponent<Graphic>();
        if (g != null) g.raycastTarget = true;

        while (btn.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(btn.onClick, 0);
        UnityEventTools.AddStringPersistentListener(btn.onClick, new UnityAction<string>(loader.LoadSceneByName), SceneName);
        EditorUtility.SetDirty(btn);
        EditorSceneManager.MarkSceneDirty(home);
        EditorSceneManager.SaveScene(home);
        return "The GadgetStatus image on Home now opens this scene.";
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------
    static TMP_InputField NameField(Transform parent, float x, float y, float w, float h)
    {
        var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = "NameInput";
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, x, y, w, h);
        go.GetComponent<Image>().color = new Color(1, 1, 1, 0); // ดูเหมือนข้อความธรรมดา

        var field = go.GetComponent<TMP_InputField>();
        field.pointSize = 40;
        var area = field.textViewport;
        if (area != null) { area.offsetMin = new Vector2(8, 0); area.offsetMax = new Vector2(-8, 0); }
        field.textComponent.fontSize = 40;
        field.textComponent.color = Grey;
        field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        if (font != null) field.textComponent.font = font;
        if (field.placeholder is TMP_Text ph)
        {
            ph.text = "Breath Gadget";
            ph.fontSize = 40;
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            ph.color = new Color(Grey.r, Grey.g, Grey.b, 0.35f);
            if (font != null) ph.font = font;
        }
        field.text = "Breath Gadget";
        return field;
    }

    static TMP_FontAsset FindThaiFont()
    {
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
            AssetDatabase.CreateFolder("Assets/element", "Gadget");
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
        img.raycastTarget = false;
        Place(img.rectTransform, x, y, w, h);
        return img;
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
