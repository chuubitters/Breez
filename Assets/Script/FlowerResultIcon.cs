using UnityEngine;

// ไอคอนดอกไม้หน้าผลลัพธ์: เด้งเข้ามาตามลำดับ แล้วโยก/หมุนเบาๆ (ทำงานตอน timeScale = 0)
public class FlowerResultIcon : MonoBehaviour
{
    public float delay;
    float start = -1f;
    Vector3 baseScale;

    void OnEnable()
    {
        if (baseScale == Vector3.zero) baseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
        start = Time.unscaledTime;
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        float t = Time.unscaledTime - start - delay;
        if (t < 0f) { transform.localScale = Vector3.zero; return; }
        float pop = t < 0.45f ? EaseOutBack(t / 0.45f) : 1f;
        float pulse = 1f + Mathf.Sin(t * 3f) * 0.04f;
        transform.localScale = baseScale * pop * pulse;
        transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.8f) * 10f);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
