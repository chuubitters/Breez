using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// =====================================================================
// ฟอนต์ทั้งเกม: LINE Seed Sans TH App (ไทย + อังกฤษ)
// ใช้เวอร์ชันที่จัดตำแหน่งวรรณยุกต์ไว้แล้ว ชื่อ "Breez Seed TH" (Regular + Bold)
// เพราะ TextMeshPro วางวรรณยุกต์/สระบนภาษาไทยเองไม่ได้ (ดู tools/thai-font)
//
// Breez > Install LINE Seed Font (ทั้งเกม)
//   1) สร้าง TMP font asset (Dynamic): Breez Seed TH SDF / Breez Seed TH Bold SDF
//   2) ตั้งเป็นฟอนต์เริ่มต้น + fallback ของ TextMeshPro
//   3) เปลี่ยนฟอนต์ข้อความ TMP ทุกอันในทุก scene + prefab
//        - ข้อความตัวหนา (Bold) หรือเดิมใช้ Montserrat => Bold
//        - นอกนั้น => Regular
//      และใส่ ThaiText ให้ (ยกเว้นช่องพิมพ์ TMP_InputField)
//   4) ลบฟอนต์เก่า (Breez Thai / Montserrat) ที่ไม่ใช้แล้ว
// กดซ้ำได้
// =====================================================================
public static class BreezThaiFontInstaller
{
    const string Folder = "Assets/Fonts/Breez";
    const string RegularTtf = Folder + "/BreezSeedTH-Regular.ttf";
    const string BoldTtf = Folder + "/BreezSeedTH-Bold.ttf";
    public const string ThaiAssetPath = Folder + "/Breez Seed TH SDF.asset";
    public const string BoldAssetPath = Folder + "/Breez Seed TH Bold SDF.asset";

    // ฟอนต์รุ่นก่อน (Prompt / Montserrat) ใช้ตารางจัดวรรณยุกต์คนละชุด => ลบทิ้งหลังย้ายเสร็จ
    static readonly string[] OldAssets =
    {
        Folder + "/Breez Thai SDF.asset", Folder + "/Montserrat SDF.asset",
        Folder + "/BreezThai-Regular.ttf", Folder + "/Montserrat-SemiBold.ttf",
        Folder + "/OFL-Prompt.txt", Folder + "/OFL-Montserrat.txt",
    };
    const string OldLatinAsset = Folder + "/Montserrat SDF.asset";

    [MenuItem("Breez/Install LINE Seed Font (ทั้งเกม)")]
    public static void InstallMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var regular = EnsureFonts();
        if (regular == null) return;
        int n = ApplyToProject();
        int removed = RemoveOldFonts();
        EditorUtility.DisplayDialog("Breez",
            "เปลี่ยนฟอนต์ทั้งเกมเป็น LINE Seed Sans TH App แล้ว\n\n" +
            "• Regular: " + ThaiAssetPath + "\n• Bold: " + BoldAssetPath +
            "\n• ตั้งเป็นฟอนต์เริ่มต้นของ TextMeshPro" +
            "\n• อัปเดตข้อความ " + n + " อัน" +
            (removed > 0 ? "\n• ลบฟอนต์เก่า " + removed + " ไฟล์" : ""), "OK");
    }

    public static TMP_FontAsset ThaiFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ThaiAssetPath);
    public static TMP_FontAsset BoldFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldAssetPath);

    /// <summary>สร้าง/โหลดฟอนต์ Regular + Bold และตั้งค่า TMP Settings คืนค่า Regular</summary>
    public static TMP_FontAsset EnsureFonts()
    {
        var regular = Ensure(RegularTtf, ThaiAssetPath);
        var bold = Ensure(BoldTtf, BoldAssetPath);
        if (regular == null) return null;
        if (bold != null) AddFallback(bold, regular);
        ConfigureSettings(regular);
        return regular;
    }

    static void AddFallback(TMP_FontAsset font, TMP_FontAsset fallback)
    {
        if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
        if (font.fallbackFontAssetTable.Contains(fallback)) return;
        font.fallbackFontAssetTable.Add(fallback);
        EditorUtility.SetDirty(font);
    }

    static void ConfigureSettings(TMP_FontAsset regular)
    {
        var settings = TMP_Settings.instance;
        if (settings == null) return;
        var so = new SerializedObject(settings);
        bool changed = false;

        var def = so.FindProperty("m_defaultFontAsset");
        if (def != null && def.objectReferenceValue != regular) { def.objectReferenceValue = regular; changed = true; }

        var list = so.FindProperty("m_fallbackFontAssets");
        if (list != null)
        {
            bool has = false;
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var el = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (el == regular) has = true;
                else if (el == null || IsOld(AssetDatabase.GetAssetPath(el))) { list.DeleteArrayElementAtIndex(i); changed = true; }
            }
            if (!has)
            {
                list.InsertArrayElementAtIndex(0);
                list.GetArrayElementAtIndex(0).objectReferenceValue = regular;
                changed = true;
            }
        }
        if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets(); }
    }

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

    /// <summary>เปลี่ยนฟอนต์ + ใส่ ThaiText ให้ทุก scene ใน Assets/Scenes และทุก prefab</summary>
    public static int ApplyToProject()
    {
        int count = 0;
        string previous = EditorSceneManager.GetActiveScene().path;

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/TextMesh Pro")) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            int c = Apply(root);
            if (c > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            count += c;
        }

        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int c = 0;
            foreach (var go in scene.GetRootGameObjects()) c += Apply(go);
            if (c > 0) EditorSceneManager.SaveScene(scene);
            count += c;
        }

        if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        return count;
    }

    /// <summary>
    /// ใช้ฟอนต์ LINE Seed กับ TMP ทุกตัวใต้ root และใส่ ThaiText (ไม่รวมช่องพิมพ์)
    /// คืนค่าจำนวนข้อความที่เปลี่ยน
    /// </summary>
    public static int Apply(GameObject root)
    {
        var regular = ThaiFont;
        if (regular == null) regular = EnsureFonts();
        var bold = BoldFont;
        if (bold == null) bold = regular;
        if (regular == null) return 0;

        int c = BreezThaiTranslator.ApplyComponents(root);
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            bool changed = BreezThaiTranslator.Apply(t);
            string oldPath = t.font != null ? AssetDatabase.GetAssetPath(t.font) : "";
            bool wantBold = (t.fontStyle & FontStyles.Bold) != 0 || oldPath == OldLatinAsset || oldPath == BoldAssetPath;
            var target = wantBold ? bold : regular;

            if (t.font != target || (t.fontStyle & FontStyles.Bold) != 0)
            {
                Undo.RecordObject(t, "LINE Seed font");
                t.font = target;
                t.fontSharedMaterial = target.material;
                if (target == bold) t.fontStyle &= ~FontStyles.Bold;   // ใช้ตัวหนาจริงแทนตัวหนาปลอม
                EditorUtility.SetDirty(t);
                changed = true;
            }

            var input = t.GetComponentInParent<TMP_InputField>(true);
            if (input != null)
            {
                if (input.fontAsset != regular) { Undo.RecordObject(input, "LINE Seed font"); input.fontAsset = regular; EditorUtility.SetDirty(input); }
            }
            else if (t.GetComponent<ThaiText>() == null)
            {
                Undo.AddComponent<ThaiText>(t.gameObject);
                changed = true;
            }
            if (changed) c++;
        }
        return c;
    }

    // เดิมเป็น Apply(root, font) — คงไว้ให้โค้ดเก่าเรียกได้
    public static int Apply(GameObject root, TMP_FontAsset ignored) => Apply(root);

    static bool IsOld(string path)
    {
        foreach (var p in OldAssets) if (p == path) return true;
        return false;
    }

    static int RemoveOldFonts()
    {
        int n = 0;
        foreach (var p in OldAssets)
            if (File.Exists(p) && AssetDatabase.DeleteAsset(p)) n++;
        if (n > 0) AssetDatabase.Refresh();
        return n;
    }
}
