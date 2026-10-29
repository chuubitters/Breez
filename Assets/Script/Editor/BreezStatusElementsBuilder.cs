using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// Element ใช้ซ้ำได้หลายหน้า
//   Breez > Create Status Elements
//       สร้าง prefab: Assets/Prefabs/Breez/BreathStatus.prefab  (status : หายใจเข้า/ออก)
//                     Assets/Prefabs/Breez/GadgetStatus.prefab  ([ไอคอน] Breath Gadget · เชื่อมต่อแล้ว)
//   Breez > Add Breath Status To Open Scene   วางป้ายหายใจที่ด้านล่างกลางจอ
//   Breez > Add Gadget Status To Open Scene   วางป้ายอุปกรณ์ที่ด้านบนกลางจอ
//   Breez > Add Status Elements To Game Scenes  ใส่ให้ Home + เกมทั้ง 3 ในครั้งเดียว
// =====================================================================
public static class BreezStatusElementsBuilder
{
    const string PrefabFolder = "Assets/Prefabs/Breez";
    const string BreathPrefab = PrefabFolder + "/BreathStatus.prefab";
    const string GadgetPrefab = PrefabFolder + "/GadgetStatus.prefab";
    const string ArtFolder = "Assets/element/UI";

    static readonly string[] GameScenes = { "Assets/Scenes/PowerRelease.unity", "Assets/Scenes/BreathFocus.unity", "Assets/Scenes/Rainy Forest.unity" };
    const string HomeScene = "Assets/Scenes/Home.unity";

    static readonly Color Ink = new Color32(34, 34, 34, 255);
    static readonly Color Grey = new Color32(85, 85, 85, 255);

    [MenuItem("Breez/Create Status Elements")]
    public static void CreatePrefabs() => CreatePrefabs(true);

    internal static void CreatePrefabs(bool showDialog)
    {
        var font = FindThaiFont();
        bool thai = font != null;

        var pill = MakeSprite("ui_pill", BreezSpriteArt.RoundedRect(128, 63), 128, 63);
        var circle = MakeSprite("ui_circle", BreezSpriteArt.Circle(256), 256, 0);
        var icons = new GadgetStatusUI.IconSprite[BreezSpriteArt.GadgetIcons.Length];
        for (int i = 0; i < icons.Length; i++)
        {
            string id = BreezSpriteArt.GadgetIcons[i];
            icons[i] = new GadgetStatusUI.IconSprite { id = id, sprite = MakeSprite($"icon_{id}", BreezSpriteArt.GadgetIcon(id, 192), 192, 0) };
        }
        EnsureFolder(PrefabFolder);

        // ---------------- BreathStatus ----------------
        var breath = NewUI("BreathStatus", null, 458, 90);
        var bg = breath.AddComponent<Image>();
        bg.sprite = pill; bg.type = Image.Type.Sliced; bg.color = Color.white;
        AddShadow(breath);

        var fillGO = NewUI("LevelFill", breath.transform, 360, 6);
        var fillRt = (RectTransform)fillGO.transform;
        fillRt.anchorMin = fillRt.anchorMax = new Vector2(0.5f, 0f);
        fillRt.anchoredPosition = new Vector2(0, 12);
        var fill = fillGO.AddComponent<Image>();
        fill.sprite = pill; fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0f;
        fill.color = new Color32(176, 214, 230, 255);
        fill.raycastTarget = false;

        var label = AddText(breath.transform, "Label", font, 40, Ink, 0, 0, 420, 70, TextAlignmentOptions.Center);
        var bs = breath.AddComponent<BreathStatusUI>();
        bs.label = label;
        bs.background = bg;
        bs.levelFill = fill;
        if (!thai) { bs.inhaleText = "Inhale"; bs.exhaleText = "Exhale"; }
        label.text = bs.prefix + "<b>" + bs.inhaleText + "</b>";
        SavePrefab(breath, BreathPrefab);

        // ---------------- GadgetStatus ----------------
        var gadget = NewUI("GadgetStatus", null, 500, 96);
        var gbg = gadget.AddComponent<Image>();
        gbg.sprite = pill; gbg.type = Image.Type.Sliced; gbg.color = Color.white;
        var outline = gadget.AddComponent<Outline>();
        outline.effectColor = new Color32(228, 228, 228, 255);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        AddShadow(gadget);
        var btn = gadget.AddComponent<Button>();
        btn.targetGraphic = gbg;

        var ring = NewUI("IconRing", gadget.transform, 66, 66);
        Anchor(ring, 0f, 15 + 33, 0);
        var ringImg = ring.AddComponent<Image>(); ringImg.sprite = circle; ringImg.color = new Color32(236, 236, 236, 255); ringImg.raycastTarget = false;
        var inner = NewUI("Inner", ring.transform, 60, 60);
        var innerImg = inner.AddComponent<Image>(); innerImg.sprite = circle; innerImg.color = Color.white; innerImg.raycastTarget = false;
        var iconGO = NewUI("Icon", ring.transform, 40, 40);
        var iconImg = iconGO.AddComponent<Image>(); iconImg.sprite = icons[0].sprite; iconImg.color = Grey; iconImg.preserveAspect = true; iconImg.raycastTarget = false;
        var dotGO = NewUI("StatusDot", ring.transform, 16, 16);
        ((RectTransform)dotGO.transform).anchoredPosition = new Vector2(23, -23);
        var dot = dotGO.AddComponent<Image>(); dot.sprite = circle; dot.color = new Color32(190, 190, 190, 255); dot.raycastTarget = false;
        var dotOutline = dotGO.AddComponent<Outline>(); dotOutline.effectColor = Color.white; dotOutline.effectDistance = new Vector2(2, -2);

        var glabel = AddText(gadget.transform, "Label", font, 22, Grey, 0, 0, 0, 60, TextAlignmentOptions.Center);
        var glRt = glabel.rectTransform;
        glRt.anchorMin = new Vector2(0, 0.5f); glRt.anchorMax = new Vector2(1, 0.5f);
        glRt.offsetMin = new Vector2(96, -30); glRt.offsetMax = new Vector2(-28, 30);
        glabel.enableWordWrapping = false;
        glabel.overflowMode = TextOverflowModes.Ellipsis;

        var battery = AddText(gadget.transform, "Battery", font, 36, Grey, 0, 0, 100, 60, TextAlignmentOptions.MidlineRight);
        Anchor(battery.gameObject, 1f, -24 - 50, 0);
        battery.text = "78%";
        battery.gameObject.SetActive(false);

        var gs = gadget.AddComponent<GadgetStatusUI>();
        gs.iconImage = iconImg; gs.label = glabel; gs.batteryText = battery; gs.statusDot = dot; gs.icons = icons;
        if (!thai) { gs.connectedText = "Connected"; gs.disconnectedText = "Not connected"; }
        glabel.text = gs.defaultName + gs.separator + gs.connectedText;
        SavePrefab(gadget, GadgetPrefab);

        AssetDatabase.SaveAssets();
        if (showDialog)
            EditorUtility.DisplayDialog("Breez", "Created:\n" + BreathPrefab + "\n" + GadgetPrefab +
                "\n\nDrag them into any Canvas, or use 'Add … To Open Scene'." +
                (thai ? "" : "\n\nNo Thai font found — using English labels (edit the text fields on the components, or add a Thai TMP font and run this again)."), "OK");
    }

    // ---------------------------------------------------------------
    [MenuItem("Breez/Add Breath Status To Open Scene")]
    public static void AddBreathToScene() => Select(AddToScene(BreathPrefab, bottom: true));

    [MenuItem("Breez/Add Gadget Status To Open Scene")]
    public static void AddGadgetToScene() => Select(AddToScene(GadgetPrefab, bottom: false));

    [MenuItem("Breez/Add Status Elements To Game Scenes")]
    public static void AddToGameScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(BreathPrefab) || !File.Exists(GadgetPrefab)) CreatePrefabs(false);
        string previous = EditorSceneManager.GetActiveScene().path;
        var log = new System.Text.StringBuilder();

        foreach (var path in GameScenes)
        {
            if (!File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var r = AddToScene(BreathPrefab, bottom: true);
            if (r != null) { EditorSceneManager.SaveScene(scene); log.AppendLine("✓ Breath status → " + Path.GetFileNameWithoutExtension(path)); }
        }
        if (File.Exists(HomeScene))
        {
            var scene = EditorSceneManager.OpenScene(HomeScene, OpenSceneMode.Single);
            var r = AddToScene(GadgetPrefab, bottom: false);
            if (r != null) { EditorSceneManager.SaveScene(scene); log.AppendLine("✓ Gadget status → Home"); }
        }
        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

        EditorUtility.DisplayDialog("Breez", log.Length > 0 ? log.ToString() + "\nMove them anywhere you like." : "Nothing added (already present or no Canvas found).", "OK");
    }

    static GameObject AddToScene(string prefabPath, bool bottom)
    {
        if (!File.Exists(prefabPath)) CreatePrefabs(false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var scene = EditorSceneManager.GetActiveScene();

        // มีอยู่แล้ว ไม่ใส่ซ้ำ
        string name = Path.GetFileNameWithoutExtension(prefabPath);
        foreach (var t in Resources.FindObjectsOfTypeAll<RectTransform>())
            if (t.gameObject.scene == scene && t.gameObject.name == name) return null;

        Canvas canvas = null;
        foreach (var c in Object.FindObjectsOfType<Canvas>())
            if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace) { canvas = c; break; }
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Breez", "No Canvas in this scene. Add a UI Canvas first.", "OK");
            return null;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, bottom ? 0f : 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, bottom ? 150 : -126);
        go.transform.SetAsLastSibling();

        // ใช้ threshold เดียวกับ GameManager ของเกมใน scene นี้
        var bs = go.GetComponent<BreathStatusUI>();
        if (bs != null)
        {
            foreach (var mb in Object.FindObjectsOfType<MonoBehaviour>())
            {
                var f = mb.GetType().GetField("breathThreshold", BindingFlags.Public | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(int)) { bs.threshold = (int)f.GetValue(mb); break; }
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(bs);
        }

        Undo.RegisterCreatedObjectUndo(go, "Add " + name);
        EditorSceneManager.MarkSceneDirty(scene);
        return go;
    }

    static void Select(GameObject go) { if (go != null) Selection.activeGameObject = go; }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------
    static GameObject NewUI(string name, Transform parent, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(w, h);
        return go;
    }

    /// <summary>วางตามแนวนอน: ax = 0 ชิดซ้าย / 1 ชิดขวา, x = ตำแหน่งจุดกึ่งกลาง</summary>
    static void Anchor(GameObject go, float ax, float x, float y)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(ax, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
    }

    static void AddShadow(GameObject go)
    {
        var s = go.AddComponent<Shadow>();
        s.effectColor = new Color(0, 0, 0, 0.12f);
        s.effectDistance = new Vector2(0, -4);
    }

    static TextMeshProUGUI AddText(Transform parent, string name, TMP_FontAsset font, float size, Color color,
                                   float x, float y, float w, float h, TextAlignmentOptions align)
    {
        var go = NewUI(name, parent, w, h);
        ((RectTransform)go.transform).anchoredPosition = new Vector2(x, y);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    static void SavePrefab(GameObject go, string path)
    {
        BreezThaiFontInstaller.Apply(go);
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

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

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    internal static Sprite MakeSprite(string name, float[] rgba, int size, int border)
    {
        EnsureFolder(ArtFolder);
        string path = $"{ArtFolder}/{name}.png";
        if (!File.Exists(path))
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
