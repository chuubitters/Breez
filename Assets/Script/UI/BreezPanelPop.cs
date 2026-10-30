using UnityEngine;

// =====================================================================
// แผง (วิธีการเล่น / ผลลัพธ์ / พัก) เวลาเปิด: ค่อยๆ ปรากฏ + การ์ดเด้งขึ้นเล็กน้อย
// และบังคับให้อยู่บนสุดเสมอ (Canvas override sorting)
// BreezUIPolish ใส่ให้อัตโนมัติ
// =====================================================================
[DisallowMultipleComponent]
public class BreezPanelPop : MonoBehaviour
{
    public float duration = 0.28f;
    public int sortingOrder = 300;

    CanvasGroup group;
    RectTransform card;
    Vector3 cardScale = Vector3.one;
    float t = 1f;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        card = FindCard();
        if (card != null) cardScale = card.localScale;
        EnsureOnTop();
    }

    RectTransform FindCard()
    {
        var c = transform.Find("Card");
        if (c == null && transform.childCount > 0)
        {
            // การ์ดใบใหญ่สุดที่เป็นลูกโดยตรง
            float best = 0f;
            foreach (Transform ch in transform)
            {
                var rt = ch as RectTransform;
                if (rt == null) continue;
                float area = rt.rect.width * rt.rect.height;
                if (area > best && rt.rect.width < 1900f) { best = area; c = rt; }
            }
        }
        return c as RectTransform;
    }

    /// <summary>มี Canvas ลูกที่ override sorting => วาดทับทุกอย่างใน scene (รวมถึงลูกโป่ง เมฆ ตัวละคร)</summary>
    public void EnsureOnTop()
    {
        var parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
        if (parentCanvas == null) return;
        var cv = GetComponent<Canvas>();
        if (cv == null) cv = gameObject.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = Mathf.Max(sortingOrder, parentCanvas.rootCanvas.sortingOrder + 1);
        if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    void OnEnable()
    {
        transform.SetAsLastSibling();
        t = 0f;
        Apply();
    }

    void OnDisable()
    {
        if (group != null) group.alpha = 1f;
        if (card != null) card.localScale = cardScale;
    }

    void Update()
    {
        if (t >= 1f) return;
        t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
        Apply();
    }

    void Apply()
    {
        float k = 1f - Mathf.Pow(1f - t, 3f);
        if (group != null) group.alpha = k;
        if (card != null)
        {
            float back = 1f + Mathf.Sin(t * Mathf.PI) * 0.025f;
            card.localScale = cardScale * Mathf.Lerp(0.92f, 1f, k) * back;
        }
    }
}
