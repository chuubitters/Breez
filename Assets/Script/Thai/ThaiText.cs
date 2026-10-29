using TMPro;
using UnityEngine;

// =====================================================================
// ใส่คู่กับ TextMeshPro เพื่อให้ภาษาไทยแสดงถูก (วรรณยุกต์ไม่ลอย/ไม่ทับสระ)
// ข้อความในช่อง Text ยังเป็นภาษาไทยปกติ แก้ได้ตามเดิม ตั้งค่าจากโค้ดด้วย .text ได้เลย
// ต้องใช้ฟอนต์ "Breez Thai SDF" (หรือมี Breez Thai เป็น fallback) — เมนู Breez > Install Thai Font
// =====================================================================
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class ThaiText : MonoBehaviour, ITextPreprocessor
{
    TMP_Text tmp;

    public string PreprocessText(string text) => ThaiShaper.Fix(text);

    void OnEnable()
    {
        tmp = GetComponent<TMP_Text>();
        if (tmp == null) return;
        tmp.textPreprocessor = this;
        tmp.havePropertiesChanged = true;
        tmp.SetAllDirty();
    }

    void OnDisable()
    {
        if (tmp == null || !ReferenceEquals(tmp.textPreprocessor, this)) return;
        tmp.textPreprocessor = null;
        tmp.havePropertiesChanged = true;
        tmp.SetAllDirty();
    }

    /// <summary>ใส่ ThaiText ให้ TMP ทุกตัวใต้ root (ใช้กับ UI ที่สร้างจากโค้ด)</summary>
    public static void AddToAll(GameObject root)
    {
        if (root == null) return;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            if (t.GetComponentInParent<TMP_InputField>() == null && t.GetComponent<ThaiText>() == null)
                t.gameObject.AddComponent<ThaiText>();
    }
}
