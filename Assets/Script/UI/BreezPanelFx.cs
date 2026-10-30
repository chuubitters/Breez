using UnityEngine;

/// <summary>หน้าต่าง: เปิดแล้วจางเข้า + การ์ดเด้งขึ้น และย้ายไปอยู่บนสุดของ Canvas</summary>
public class BreezPanelFx : MonoBehaviour
{
    public float duration = 0.28f;
    CanvasGroup group;
    RectTransform card;
    float t = 1f;

    void OnEnable()
    {
        transform.SetAsLastSibling();
        if (transform.parent != null && BreezUiFx.PanelNames.Contains(transform.parent.name)) transform.parent.SetAsLastSibling();
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        if (card == null)
        {
            var c = transform.Find("Card");
            if (c == null) c = transform.Find("Panel/Card");
            if (c == null && transform.childCount > 0) c = transform.GetChild(0);
            card = c as RectTransform;
        }
        t = 0f;
        Apply();
    }

    void Update()
    {
        if (t >= 1f) return;
        t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.05f, duration));
        Apply();
    }

    void Apply()
    {
        // ease-out-back เล็กน้อย
        float e = 1f - Mathf.Pow(1f - t, 3f);
        float back = Mathf.Sin(t * Mathf.PI) * 0.035f;
        if (group != null) group.alpha = Mathf.Clamp01(t * 2.2f);
        if (card != null) card.localScale = Vector3.one * (Mathf.Lerp(0.93f, 1f, e) + back);
    }
}
