using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>ปุ่ม: hover ขยาย / กดยุบ (ใช้เวลาจริง ทำงานแม้เกมหยุด)</summary>
public class BreezButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public float hoverScale = 1.05f;
    public float pressScale = 0.94f;
    public float speed = 14f;

    Vector3 baseScale, lastApplied;
    bool hover, down;
    float k = 1f;
    Selectable sel;

    void Awake()
    {
        sel = GetComponent<Selectable>();
        baseScale = lastApplied = transform.localScale;
    }

    void OnDisable()
    {
        hover = down = false;
        k = 1f;
        if (lastApplied == transform.localScale) transform.localScale = baseScale;
        lastApplied = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData e) => hover = true;
    public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
    public void OnPointerDown(PointerEventData e) => down = true;
    public void OnPointerUp(PointerEventData e) => down = false;

    void LateUpdate()
    {
        // สคริปต์อื่นเปลี่ยนขนาดปุ่มเอง (เช่น ปุ่มอารมณ์ที่เลือกอยู่) => ใช้ขนาดนั้นเป็นฐานใหม่
        if (transform.localScale != lastApplied) baseScale = transform.localScale / Mathf.Max(0.0001f, k);

        bool active = sel == null || sel.IsInteractable();
        float target = !active ? 1f : down ? pressScale : hover ? hoverScale : 1f;
        k = Mathf.Lerp(k, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
        if (Mathf.Abs(k - target) < 0.001f) k = target;
        lastApplied = baseScale * k;
        transform.localScale = lastApplied;
    }
}
