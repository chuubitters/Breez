using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================
// ปุ่มนุ่มๆ: ชี้ = ขยายเล็กน้อย, กด = ยุบ, ปล่อย = เด้งกลับ (ใช้ unscaled time ทำงานตอนเกมหยุดได้)
// ใส่ให้ทุกปุ่มอัตโนมัติ (BreezUIPolish) ไม่อยากได้ใส่ BreezNoFx ที่ปุ่มนั้น
// =====================================================================
[DisallowMultipleComponent]
public class BreezButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public float hoverScale = 1.05f;
    public float pressScale = 0.94f;
    public float speed = 14f;

    Button button;
    Vector3 baseScale = Vector3.one, lastWritten;
    float target = 1f, current = 1f, kick;
    bool hover, down;

    void Awake()
    {
        button = GetComponent<Button>();
        baseScale = transform.localScale;
        if (baseScale == Vector3.zero) baseScale = Vector3.one;
        lastWritten = baseScale;
    }

    void OnDisable()
    {
        hover = down = false;
        current = target = 1f; kick = 0f;
        transform.localScale = baseScale;
        lastWritten = baseScale;
    }

    bool Active => button == null || button.IsInteractable();

    public void OnPointerEnter(PointerEventData e) { hover = true; }
    public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
    public void OnPointerDown(PointerEventData e) { down = true; }
    public void OnPointerUp(PointerEventData e)
    {
        if (down && Active) kick = 0.06f;   // เด้งเล็กน้อยตอนปล่อย
        down = false;
    }

    void Update()
    {
        target = !Active ? 1f : down ? pressScale : hover ? hoverScale : 1f;
        float dt = Time.unscaledDeltaTime;
        current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
        kick = Mathf.MoveTowards(kick, 0f, dt * 0.4f);
        float s = current + Mathf.Sin(kick / 0.06f * Mathf.PI) * kick;
        // สคริปต์อื่นเปลี่ยนขนาดปุ่ม (เช่น ปุ่มอารมณ์ที่เลือกใหญ่ขึ้น) => ใช้ขนาดนั้นเป็นฐานใหม่
        if (transform.localScale != lastWritten) baseScale = transform.localScale;
        if (Mathf.Abs(s - 1f) < 0.0005f && transform.localScale == baseScale) { lastWritten = baseScale; return; }
        lastWritten = baseScale * s;
        transform.localScale = lastWritten;
    }
}
