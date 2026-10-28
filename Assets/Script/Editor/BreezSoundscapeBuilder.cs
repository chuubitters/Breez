using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// =====================================================================
// Soundscape
//   Breez > Upgrade Rainy Forest Rain
//       ใส่ BreathRain ให้ RainEffect: ฝนเม็ดเล็ก ตกเมื่อเป่า ยิ่งเป่าแรงยิ่งตกหนัก
//   Breez > Create Windy Mountain Scene
//       คัดลอก Rainy Forest (ระบบหายใจ 5-5, start, countdown, บันทึก session)
//       แล้วเปลี่ยนฉากเป็นภูเขา + เงาดอกแดนดิไลออน (เป่า = เมล็ดปลิว)
//       เสียง BGM / SFX เว้นว่างไว้ให้ใส่เอง
//       และผูกปุ่ม "Windy Mountain" ในหน้าเลือก Soundscape
// =====================================================================
public static class BreezSoundscapeBuilder
{
    const string RainyPath = "Assets/Scenes/Rainy Forest.unity";
    const string WindyPath = "Assets/Scenes/Windy Mountain.unity";
    const string WindyName = "Windy Mountain";
    const string ArtFolder = "Assets/element/WM";

    static readonly Color Silhouette = new Color32(0x6F, 0x8B, 0x72, 235);
    static readonly Color SeedColor = new Color32(0x7E, 0x9A, 0x80, 220);

    // ---------------------------------------------------------------
    [MenuItem("Breez/Upgrade Rainy Forest Rain")]
    public static void UpgradeRain()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(RainyPath)) { EditorUtility.DisplayDialog("Breez", "Rainy Forest scene not found.", "OK"); return; }
        var scene = EditorSceneManager.OpenScene(RainyPath, OpenSceneMode.Single);

        var ps = FindRain();
        if (ps == null) { EditorUtility.DisplayDialog("Breez", "No ParticleSystem (RainEffect) found in Rainy Forest.", "OK"); return; }
        var rain = ps.GetComponent<BreathRain>();
        if (rain == null) rain = ps.gameObject.AddComponent<BreathRain>(); // อย่าใช้ ?? กับ Component ของ Unity
        var sp = Object.FindObjectOfType<SPGameManager>();
        if (sp != null) rain.threshold = sp.breathThreshold;
        rain.breathReceiver = Object.FindObjectOfType<BreathReceiver>();

        EditorUtility.SetDirty(rain);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = ps.gameObject;
        EditorUtility.DisplayDialog("Breez", "Rainy Forest now uses fine drizzle (BreathRain on " + ps.name + ").\n\nPress Play and blow — harder blowing = heavier rain.\nTweak drop size / rate on the BreathRain component.", "OK");
    }

    // ---------------------------------------------------------------
    [MenuItem("Breez/Create Windy Mountain Scene")]
    public static void CreateWindy()
    {
        if (!File.Exists(RainyPath)) { EditorUtility.DisplayDialog("Breez", "Rainy Forest scene not found (it is used as the template).", "OK"); return; }
        if (File.Exists(WindyPath) &&
            !EditorUtility.DisplayDialog("Breez", "Windy Mountain.unity already exists. Replace it?", "Replace", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // ---------- รูป ----------
        var bgSprite = MakeSprite("wm_background", BreezSpriteArt.MountainBackground(1024, 576), 1024, 576, 57.6f, new Vector2(0.5f, 0.5f));
        var seedSprite = MakeSprite("wm_seed", BreezSpriteArt.DandelionSeed(128), 128, 128, 160f, new Vector2(0.5f, 0f));
        var leafSprite = MakeSprite("wm_leaf", BreezSpriteArt.DandelionLeaf(128), 128, 128, 85f, new Vector2(0.5f, 0f));
        var stemSprite = MakeSprite("wm_stem", BreezSpriteArt.Rect(8, 128, 4), 8, 128, 128f, new Vector2(0.5f, 0f));
        var coreSprite = MakeSprite("wm_core", BreezSpriteArt.Circle(128), 128, 128, 128f, new Vector2(0.5f, 0.5f));

        // ---------- คัดลอกฉาก ----------
        if (File.Exists(WindyPath)) AssetDatabase.DeleteAsset(WindyPath);
        AssetDatabase.CopyAsset(RainyPath, WindyPath);
        var scene = EditorSceneManager.OpenScene(WindyPath, OpenSceneMode.Single);

        // ปิดฝน
        var rain = FindRain();
        if (rain != null) rain.gameObject.SetActive(false);

        // ปิดรูปพื้นหลังป่าใน Canvas (จะใช้ฉากภูเขาแทน)
        foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
        {
            if (img.gameObject.scene != scene || img.sprite == null) continue;
            if (img.sprite.name.StartsWith("Slide 16_9") || img.sprite.name.ToLower().Contains("rain"))
                img.gameObject.SetActive(false);
        }
        // เปลี่ยนชื่อในข้อความ
        foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            if (t.gameObject.scene == scene && t.text.Contains("Rainy Forest"))
            { t.text = t.text.Replace("Rainy Forest", WindyName); EditorUtility.SetDirty(t); }

        // เสียง: เว้นไว้ให้ใส่เอง
        var sp = Object.FindObjectOfType<SPGameManager>();
        if (sp != null)
        {
            sp.rainParticles = null;
            if (sp.bgm != null) { sp.bgm.clip = null; EditorUtility.SetDirty(sp.bgm); }
            if (sp.sfx != null) { sp.sfx.clip = null; EditorUtility.SetDirty(sp.sfx); }
            EditorUtility.SetDirty(sp);
        }

        // ---------- ฉากภูเขา + แดนดิไลออน ----------
        var cam = Camera.main;
        Vector3 c = cam != null ? cam.transform.position : new Vector3(0, 0, -10);
        float halfH = cam != null && cam.orthographic ? cam.orthographicSize : 5f;
        float k = halfH / 5f; // ปรับตามขนาดกล้อง

        var world = new GameObject("Windy Mountain World");
        world.transform.position = new Vector3(c.x, c.y, 0f);

        var bg = new GameObject("Background (replace me)");
        bg.transform.SetParent(world.transform, false);
        bg.transform.localPosition = new Vector3(0, 0, 5f);
        bg.transform.localScale = Vector3.one * k;
        var bgr = bg.AddComponent<SpriteRenderer>();
        bgr.sprite = bgSprite; bgr.sortingOrder = -20;

        var plants = new[]
        {
            new { x = -7.4f, h = 4.2f, tilt = 6f,  r = 0.85f },
            new { x = -6.1f, h = 5.6f, tilt = -3f, r = 1.0f },
            new { x = -4.7f, h = 3.3f, tilt = 4f,  r = 0.7f },
            new { x = 5.0f,  h = 3.6f, tilt = -5f, r = 0.75f },
            new { x = 6.4f,  h = 5.0f, tilt = 3f,  r = 0.95f },
            new { x = 7.7f,  h = 4.0f, tilt = -2f, r = 0.8f },
        };
        float baseY = -5.3f;
        var roots = new List<Transform>();
        var rnd = new System.Random(3);

        for (int p = 0; p < plants.Length; p++)
        {
            var pl = plants[p];
            var root = new GameObject($"Dandelion {p + 1}").transform;
            root.SetParent(world.transform, false);
            root.localPosition = new Vector3(pl.x * k, baseY * k, 0f);
            root.localRotation = Quaternion.Euler(0, 0, pl.tilt);
            root.localScale = Vector3.one * k;
            roots.Add(root);

            // ใบที่โคน (ไม่โยกตาม เลยวางไว้ที่ world)
            float[] leafAngles = { 38f, -30f, 62f };
            for (int i = 0; i < leafAngles.Length; i++)
            {
                var leaf = Sprite2D($"Leaf {p + 1}.{i + 1}", world.transform, leafSprite, Silhouette, -6);
                leaf.transform.localPosition = new Vector3(pl.x * k, baseY * k, 0.1f);
                leaf.transform.localRotation = Quaternion.Euler(0, 0, leafAngles[i] + pl.tilt * 0.5f);
                leaf.transform.localScale = new Vector3(0.45f / 1.5f, (1.5f + 0.3f * i) / 1.5f, 1f) * k;
            }

            var stem = Sprite2D("Stem", root, stemSprite, Silhouette, -5);
            stem.transform.localScale = new Vector3(0.06f / 0.0625f, pl.h, 1f);   // stem sprite = 0.0625 x 1 unit

            var head = new GameObject("Head").transform;
            head.SetParent(root, false);
            head.localPosition = new Vector3(0, pl.h, 0);

            var core = Sprite2D("Core", head, coreSprite, Silhouette, -4);
            core.transform.localScale = Vector3.one * 0.26f * pl.r;

            int n = Mathf.RoundToInt(34 * pl.r);
            for (int i = 0; i < n; i++)
            {
                float ang = i * 360f / n + (float)(rnd.NextDouble() * 8 - 4);
                var seed = Sprite2D($"Seed {i + 1}", head, seedSprite, SeedColor, -3);
                seed.transform.localRotation = Quaternion.Euler(0, 0, ang - 90f);
                seed.transform.localScale = Vector3.one * pl.r;
            }
        }

        var field = world.AddComponent<DandelionField>();
        field.dandelions = roots.ToArray();
        field.breathReceiver = Object.FindObjectOfType<BreathReceiver>();
        if (sp != null) field.threshold = sp.breathThreshold;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings(WindyPath);

        string wired = WireMenuButtons();
        EditorSceneManager.OpenScene(WindyPath, OpenSceneMode.Single);
        Selection.activeGameObject = GameObject.Find("Windy Mountain World");

        EditorUtility.DisplayDialog("Breez",
            "Created Assets/Scenes/Windy Mountain.unity\n\n" +
            "• Blow = dandelion seeds fly away; they grow back after you stop\n" +
            "• BGM / SFX AudioSources are left empty for your sounds\n" +
            "• Replace the background sprite on 'Background (replace me)' if you have artwork\n\n" + wired, "OK");
    }

    // ---------------------------------------------------------------
    static ParticleSystem FindRain()
    {
        var sp = Object.FindObjectOfType<SPGameManager>();
        if (sp != null && sp.rainParticles != null) return sp.rainParticles;
        foreach (var ps in Resources.FindObjectsOfTypeAll<ParticleSystem>())
            if (ps.gameObject.scene.IsValid() && ps.name.ToLower().Contains("rain")) return ps;
        return null;
    }

    static GameObject Sprite2D(string name, Transform parent, Sprite sprite, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    /// <summary>ผูกปุ่ม "Windy Mountain" ในหน้าเลือก Soundscape ให้เปิดฉากใหม่</summary>
    static string WireMenuButtons()
    {
        var report = new List<string>();
        foreach (var path in Directory.GetFiles("Assets/Scenes", "*.unity"))
        {
            string file = Path.GetFileNameWithoutExtension(path);
            if (file == WindyName || file == "Rainy Forest") continue;
            string text = File.ReadAllText(path);
            if (!text.Contains("Windy")) continue;

            var scene = EditorSceneManager.OpenScene(path.Replace('\\', '/'), OpenSceneMode.Single);
            var loader = Object.FindObjectOfType<SceneLoader>();
            if (loader == null) continue;
            bool changed = false;
            foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (b.gameObject.scene != scene || !b.gameObject.name.Replace(" ", "").StartsWith("WindyMountain")) continue;
                while (b.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(b.onClick, 0);
                UnityEventTools.AddStringPersistentListener(b.onClick, new UnityAction<string>(loader.LoadSceneByName), WindyName);
                EditorUtility.SetDirty(b);
                changed = true;
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.Add(file);
            }
        }
        return report.Count > 0
            ? "'Windy Mountain' button now opens the scene in: " + string.Join(", ", report)
            : "Could not find a 'Windy Mountain' button — set its OnClick to SceneLoader.LoadSceneByName(\"Windy Mountain\").";
    }

    static void AddToBuildSettings(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Exists(s => s.path == path)) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static Sprite MakeSprite(string name, float[] rgba, int w, int h, float ppu, Vector2 pivot)
    {
        if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/element", "WM");
        string path = $"{ArtFolder}/{name}.png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = ppu;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
