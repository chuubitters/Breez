using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// =====================================================================
// Breez > Install Thai Font
//   1) สร้าง TMP font asset แบบ Dynamic จาก Assets/Fonts/Breez
//        Breez Thai SDF      (Prompt + glyph ไทยที่จัดตำแหน่งแล้ว)
//        Montserrat SDF      (ตัวเลข/อังกฤษ ตามดีไซน์)
//   2) ใส่ Breez Thai เป็น fallback ของ TMP (ทุกฟอนต์แสดงไทยได้)
//   3) ใส่ component ThaiText ให้ข้อความ TMP ทุกอันใน scene + prefab
//      และเปลี่ยนฟอนต์ของข้อความที่มีภาษาไทยเป็น Breez Thai
// กดซ้ำได้ ไม่สร้างซ้ำ
// =====================================================================
public static class BreezThaiFontInstaller
{
    const string Folder = "Assets/Fonts/Breez";
    public const string ThaiAssetPath = Folder + "/Breez Thai SDF.asset";
    public const string LatinAssetPath = Folder + "/Montserrat SDF.asset";

    [MenuItem("Breez/Install Thai Font")]
    public static void InstallMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var thai = EnsureFonts();
        if (thai == null) return;
        int n = ApplyToProject(thai);
        EditorUtility.DisplayDialog("Breez",
            "ติดตั้งฟอนต์ไทยแล้ว\n\n• " + ThaiAssetPath + "\n• " + LatinAssetPath +
            "\n• ตั้ง Breez Thai เป็น fallback ของ TextMeshPro\n• ใส่ ThaiText ให้ข้อความ " + n + " อัน", "OK");
    }

    /// <summary>สร้าง/โหลดฟอนต์ทั้งสอง คืนค่า Breez Thai</summary>
    public static TMP_FontAsset EnsureFonts()
    {
        var thai = Ensure(Folder + "/BreezThai-Regular.ttf", ThaiAssetPath);
        Ensure(Folder + "/Montserrat-SemiBold.ttf", LatinAssetPath);
        if (thai == null) return null;

        var settings = TMP_Settings.instance;
        if (settings != null)
        {
            var list = TMP_Settings.fallbackFontAssets;
            if (list != null && !list.Contains(thai))
            {
                list.Insert(0, thai);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }
        return thai;
    }

    public static TMP_FontAsset ThaiFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ThaiAssetPath);
    public static TMP_FontAsset LatinFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LatinAssetPath);

    static TMP_FontAsset Ensure(string ttfPath, string assetPath)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            EditorUtility.DisplayDialog("Breez", "ไม่พบไฟล์ฟอนต์ " + ttfPath, "OK");
            return null;
        }

        var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (fa == null) return null;
        fa.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(fa, assetPath);

        if (fa.atlasTextures != null)
            for (int i = 0; i < fa.atlasTextures.Length; i++)
            {
                var tex = fa.atlasTextures[i];
                if (tex == null) continue;
                tex.name = fa.name + " Atlas" + (i > 0 ? " " + i : "");
                AssetDatabase.AddObjectToAsset(tex, fa);
            }
        if (fa.material != null)
        {
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
        }
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
    }

    /// <summary>ใส่ ThaiText (+ เปลี่ยนฟอนต์ข้อความไทย) ให้ทุก scene ใน Assets/Scenes และทุก prefab</summary>
    public static int ApplyToProject(TMP_FontAsset thai)
    {
        int count = 0;
        string previous = EditorSceneManager.GetActiveScene().path;

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/TextMesh Pro")) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            int c = Apply(root, thai);
            if (c > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            count += c;
        }

        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int c = 0;
            foreach (var go in scene.GetRootGameObjects()) c += Apply(go, thai);
            if (c > 0) EditorSceneManager.SaveScene(scene);
            count += c;
        }

        if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        return count;
    }

    /// <summary>ใส่ ThaiText ให้ TMP ใต้ root (ไม่รวมช่องพิมพ์ TMP_InputField)</summary>
    public static int Apply(GameObject root, TMP_FontAsset thai)
    {
        int c = 0;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.GetComponentInParent<TMP_InputField>(true) != null) continue;
            if (t.GetComponent<ThaiText>() != null) continue;
            if (thai != null && ThaiShaper.HasThai(t.text) && !IsBreezFont(t.font))
            {
                Undo.RecordObject(t, "Thai font");
                t.font = thai;
                EditorUtility.SetDirty(t);
            }
            Undo.AddComponent<ThaiText>(t.gameObject);
            c++;
        }
        return c;
    }

    static bool IsBreezFont(TMP_FontAsset f)
    {
        if (f == null) return false;
        string p = AssetDatabase.GetAssetPath(f);
        return p == ThaiAssetPath;
    }
}
