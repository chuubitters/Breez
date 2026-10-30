using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================
// Breez > Polish UI (All Scenes)   (กดซ้ำได้)
//   • ปุ่มกลับทุกหน้า => ใช้รูป back-bt เหมือนกันหมด ขนาด 249x79 มุมซ้ายบนตำแหน่งเดียวกัน
//     (ปุ่มกลับในหน้าวิธีการเล่น/ผลลัพธ์ ซ้อนตรงกับปุ่มกลับของฉากพอดี ไม่เหลื่อมกัน)
//   • Soundscape: หัวข้อ + สถานะอุปกรณ์ อยู่แถวเดียวกับปุ่มกลับ
//   • แผงวิธีการเล่น / ผลลัพธ์ / พัก ของ Breath Play อยู่ใน BreezTopCanvas (บนสุด)
//   • นับถอยหลัง Soundscape: พื้นหลังขาว
// แอนิเมชันปุ่ม/แผง ทำงานเองตอนเล่น (BreezUIPolish) ไม่ต้องกดเมนู
// =====================================================================
public static class BreezPolishUI
{
    public const string BackSpritePath = "Assets/element/back-bt.png";
    public static readonly Vector2 BackSize = new Vector2(249, 79);
    /// <summary>จุดกึ่งกลางปุ่มกลับ วัดจากมุมซ้ายบนของจอ 1920x1080</summary>
    public static readonly Vector2 BackPos = new Vector2(216, -116);

    [MenuItem("Breez/Polish UI (All Scenes)")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = EditorSceneManager.GetActiveScene().path;
        var log = new System.Text.StringBuilder();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!s.enabled || !File.Exists(s.path)) continue;
            var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            int backs = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var b in root.GetComponentsInChildren<Button>(true))
                    if (IsBack(b)) { Style(b); backs++; }

            string extra = "";
            int gadgets = ReplaceGadgetImages(scene);
            if (gadgets > 0) extra += $" · ป้ายสถานะจริง {gadgets}";
            var sp = Object.FindObjectOfType<SPGameManager>(true);
            if (sp != null) extra += AlignSoundscape(sp);
            if (sp != null && sp.countdownUI != null)
            {
                var bl = sp.countdownUI.transform.Find("Blocker");
                var img = bl != null ? bl.GetComponent<Image>() : null;
                if (img != null) { Undo.RecordObject(img, "Countdown bg"); img.color = new Color(1f, 1f, 1f, 0.92f); }
                Undo.RecordObject(sp.countdownUI, "Countdown bg");
                sp.countdownUI.backdrop = new Color(1f, 1f, 1f, 0.92f);
                EditorUtility.SetDirty(sp.countdownUI);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"✓ {Path.GetFileNameWithoutExtension(s.path)}: ปุ่มกลับ {backs}{extra}");
        }
        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Breez", "จัด UI ทุกหน้าแล้ว\n\n" + log, "OK");
    }

    // ---------------------------------------------------------------
    public static bool IsBack(Button b)
    {
        string n = b.name.ToLowerInvariant();
        if (n.Contains("prev") || n.Contains("next") || n.Contains("close")) return false;
        if (n.Contains("back")) return true;
        var img = b.GetComponent<Image>();
        if (img != null && img.sprite != null && img.sprite.name.ToLowerInvariant().Contains("back")) return true;
        foreach (var t in b.GetComponentsInChildren<TMP_Text>(true))
        {
            string s = (t.text ?? "").Trim();
            if (s == "Back" || s == "กลับ" || s == "< กลับ" || s == "‹ กลับ") return true;
        }
        return false;
    }

    /// <summary>ใช้รูป back-bt ขนาด/ตำแหน่งมาตรฐาน</summary>
    public static void Style(Button b)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackSpritePath);
        var rt = (RectTransform)b.transform;
        Undo.RecordObject(rt, "Back button");
        var img = b.GetComponent<Image>();
        if (img == null) img = Undo.AddComponent<Image>(b.gameObject);
        Undo.RecordObject(img, "Back button");
        if (sprite != null) img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = true;
        Undo.RecordObject(b, "Back button");
        b.targetGraphic = img;
        b.transition = Selectable.Transition.ColorTint;
        var cb = b.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(0.96f, 0.96f, 0.96f); cb.pressedColor = new Color(0.86f, 0.86f, 0.86f);
        b.colors = cb;

        // ลูก (ลูกศร / ข้อความ Back) ไม่ต้องใช้แล้ว — รูป back-bt มีครบ
        foreach (Transform ch in b.transform)
        {
            Undo.RecordObject(ch.gameObject, "Back button");
            ch.gameObject.SetActive(false);
        }

        if (IsFullScreen(rt.parent as RectTransform))
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = BackPos;
        }
        rt.sizeDelta = BackSize;
        rt.localScale = Vector3.one;
        rt.SetAsLastSibling();
        EditorUtility.SetDirty(b.gameObject);
    }

    static bool IsFullScreen(RectTransform p)
    {
        if (p == null) return false;
        if (p.GetComponent<Canvas>() != null) return true;
        return p.anchorMin == Vector2.zero && p.anchorMax == Vector2.one && p.offsetMin == Vector2.zero && p.offsetMax == Vector2.zero;
    }

    // ---------------------------------------------------------------
    /// <summary>รูป GadgetStatus.png (แค่ภาพ) => ป้ายสถานะจริง (prefab GadgetStatus: ชื่อ ไอคอน สี สถานะ กดแล้วไปตั้งค่า)</summary>
    public static int ReplaceGadgetImages(Scene scene)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Breez/GadgetStatus.prefab");
        if (prefab == null) return 0;
        var found = new List<Image>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var img in root.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite == null || !img.sprite.name.ToLowerInvariant().StartsWith("gadgetstatus")) continue;
                if (img.GetComponentInParent<GadgetStatusUI>(true) != null) continue;
                found.Add(img);
            }
        foreach (var img in found)
        {
            var rt = img.rectTransform;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rt.parent);
            Undo.RegisterCreatedObjectUndo(inst, "Gadget status");
            var irt = (RectTransform)inst.transform;
            irt.anchorMin = rt.anchorMin; irt.anchorMax = rt.anchorMax; irt.pivot = rt.pivot;
            irt.anchoredPosition = rt.anchoredPosition; irt.sizeDelta = rt.sizeDelta; irt.localScale = rt.localScale;
            irt.SetSiblingIndex(rt.GetSiblingIndex() + 1);
            Undo.RecordObject(img.gameObject, "Gadget status");
            img.gameObject.SetActive(false);
        }
        return found.Count;
    }

    // ---------------------------------------------------------------
    /// <summary>Soundscape: หัวข้อ + สถานะอุปกรณ์ อยู่แถวเดียวกับปุ่มกลับ</summary>
    static string AlignSoundscape(SPGameManager sp)
    {
        Canvas canvas = sp.endButton != null ? sp.endButton.GetComponentInParent<Canvas>() : null;
        if (canvas == null && sp.countdownUI != null) canvas = sp.countdownUI.GetComponentInParent<Canvas>(true);
        if (canvas == null) return "";
        var C = canvas.rootCanvas.transform;
        // แถวบน: y กึ่งกลางของปุ่มกลับ ในพิกัดกลางจอ
        float rowY = 540f + BackPos.y;
        int n = 0;
        foreach (Transform ch in C)
        {
            var rt = ch as RectTransform;
            if (rt == null || !ch.gameObject.activeSelf) continue;
            bool centered = rt.anchorMin == new Vector2(0.5f, 0.5f) && rt.anchorMax == rt.anchorMin;
            if (!centered) continue;
            var t = ch.GetComponent<TMP_Text>();
            bool isTitle = t != null && t != sp.countdownText && t != sp.breathingText && rt.anchoredPosition.y > 200f;
            bool isGadget = ch.name == "GadgetStatus";
            if (!isTitle && !isGadget) continue;
            Undo.RecordObject(rt, "Align top row");
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rowY);
            n++;
        }
        return n > 0 ? $" · จัดแถวบน {n}" : "";
    }
}
