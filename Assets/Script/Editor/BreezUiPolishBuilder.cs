using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// Breez > Polish UI (Back Buttons + Layers)   ใช้กับทุก scene ใน Build Settings (กดซ้ำได้)
//   • ปุ่มกลับ/Back ทุกปุ่ม => ใช้รูป Assets/element/back-bt.png ขนาดเดียวกัน (249x79)
//     ตำแหน่งเดียวกันทุกหน้า: มุมซ้ายบน (ไม่ทับการ์ด/หัวข้อ)  ปุ่มยังทำงานเหมือนเดิม
//   • จัดเลเยอร์: หน้าต่างวิธีการเล่น / ผลลัพธ์ / สรุป / พัก / ประวัติ / นับถอยหลัง อยู่บนสุดของ Canvas
//   • นับถอยหลัง Soundscape: พื้นหลังสีขาว
// =====================================================================
public static class BreezUiPolishBuilder
{
    const string BackSprite = "Assets/element/back-bt.png";
    static readonly Vector2 BackSize = new Vector2(249, 79);
    static readonly Vector2 BackPos = new Vector2(160, -75);   // จากมุมซ้ายบน (จุดกึ่งกลางปุ่ม)

    // ลำดับเลเยอร์ (ล่าง -> บน): ตัวท้ายสุดอยู่บนสุด
    static readonly string[] LayerOrder =
    {
        "BreathGuide", "CountdownOverlay", "HowToPlayPanel", "PausePanel",
        "ResultPanel", "SummaryPanel", "GameOverPanel", "SoundscapeResult", "DailyHistory"
    };

    [MenuItem("Breez/Polish UI (Back Buttons + Layers)")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackSprite);
        if (sprite == null) { EditorUtility.DisplayDialog("Breez", "ไม่พบ " + BackSprite, "OK"); return; }

        string previous = EditorSceneManager.GetActiveScene().path;
        var log = new System.Text.StringBuilder();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!s.enabled || !File.Exists(s.path)) continue;
            var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            int backs = 0, layers = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var b in root.GetComponentsInChildren<Button>(true))
                    if (IsBack(b)) { MakeBack(b, sprite); backs++; }
                layers += SortLayers(root.transform);
                foreach (var cd in root.GetComponentsInChildren<SoundscapeCountdownUI>(true)) WhiteCountdown(cd);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"✓ {Path.GetFileNameWithoutExtension(s.path)}  ปุ่มกลับ {backs} · เลเยอร์ {layers}");
        }
        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Breez", "จัด UI ทุกหน้าแล้ว\n\n" + log, "OK");
    }

    // ---------------------------------------------------------------
    static bool IsBack(Button b)
    {
        string n = b.name.ToLowerInvariant();
        if (n.Contains("close") || n.Contains("prev") || n.Contains("next")) return false;
        var img = b.GetComponent<Image>();
        if (img != null && img.sprite != null && img.sprite.name.ToLowerInvariant().Contains("back")) return true;
        if (n.Contains("back")) return true;
        foreach (var t in b.GetComponentsInChildren<TMP_Text>(true))
        {
            string x = (t.text ?? "").Trim().ToLowerInvariant();
            if (x == "back" || x == "กลับ" || x.EndsWith(" back") || x.EndsWith("กลับ")) return true;
        }
        return false;
    }

    static void MakeBack(Button b, Sprite sprite)
    {
        var go = b.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(go, "Back button");

        // ลบลูกที่เป็นลูกศร/ตัวหนังสือเดิม (รูป back-bt มีคำว่า "กลับ" อยู่แล้ว)
        for (int i = go.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        foreach (var sh in go.GetComponents<Shadow>()) Object.DestroyImmediate(sh);   // Outline ก็สืบจาก Shadow

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.raycastTarget = true;
        b.targetGraphic = img;
        b.transition = Selectable.Transition.ColorTint;
        var c = b.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(0.97f, 0.97f, 0.97f);
        c.pressedColor = new Color(0.88f, 0.88f, 0.88f);
        c.selectedColor = Color.white;
        b.colors = c;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = BackSize;
        rt.anchoredPosition = BackPos;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        go.transform.SetAsLastSibling();   // ไม่ให้อะไรในหน้าเดียวกันบัง
        EditorUtility.SetDirty(go);
    }

    /// <summary>เรียงหน้าต่างตาม LayerOrder ใน Canvas เดียวกัน (ตัวที่อยู่หลังสุด = บนสุด)</summary>
    static int SortLayers(Transform root)
    {
        int n = 0;
        var found = new List<Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (System.Array.IndexOf(LayerOrder, t.name) >= 0) found.Add(t);
        found.Sort((a, b) => System.Array.IndexOf(LayerOrder, a.name).CompareTo(System.Array.IndexOf(LayerOrder, b.name)));
        foreach (var t in found)
        {
            Undo.RegisterFullObjectHierarchyUndo(t.parent != null ? t.parent.gameObject : t.gameObject, "Layers");
            t.SetAsLastSibling();
            n++;
        }
        // ปุ่มกลับในหน้าหลักอยู่เหนือพื้นหลังเสมอ แต่ใต้หน้าต่างป๊อปอัป => หน้าต่างถูกย้ายไปหลังสุดแล้วด้านบน
        return n;
    }

    static void WhiteCountdown(SoundscapeCountdownUI cd)
    {
        var blocker = cd.transform.Find("Blocker");
        if (blocker == null) return;
        var img = blocker.GetComponent<Image>();
        if (img == null) return;
        Undo.RecordObject(img, "Countdown bg");
        img.color = new Color(1f, 1f, 1f, 0.94f);
        img.raycastTarget = true;
        foreach (var i in cd.GetComponentsInChildren<Image>(true))
            if (i.name == "RingTrack") { Undo.RecordObject(i, "Track"); i.color = new Color32(0xE4, 0xEC, 0xDD, 255); }
        EditorUtility.SetDirty(cd);
    }
}
