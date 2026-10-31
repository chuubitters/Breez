using UnityEngine;

// ผึ้ง: กระพือปีก (เร็วขึ้นตอนลอยขึ้น), เอียงตัวตามทิศ, ลอยขึ้นลงเล็กน้อย, เด้งตอนเก็บดอกไม้
public class BeeVisual : MonoBehaviour
{
    public Transform body;        // ลำตัว (ถ้าไม่ใส่ = ตัวเอง)
    public Transform wingFront;
    public Transform wingBack;
    public float flapSpeed = 26f;
    public float maxTilt = 18f;

    Rigidbody2D rb;
    Vector3 wingFrontScale, wingBackScale, bodyScale;
    float bump;

    void Start()
    {
        rb = GetComponentInParent<Rigidbody2D>();
        if (body == null) body = transform;
        bodyScale = body.localScale;
        if (wingFront != null) wingFrontScale = wingFront.localScale;
        if (wingBack != null) wingBackScale = wingBack.localScale;
    }

    public void Bump() { bump = 1f; }

    void Update()
    {
        float vy = rb != null ? rb.velocity.y : 0f;
        float speed = flapSpeed * (vy > 0.1f ? 1.4f : 1f);
        float t = Time.unscaledTime;
        float f = Mathf.Abs(Mathf.Sin(t * speed));
        if (wingFront != null) wingFront.localScale = new Vector3(wingFrontScale.x, wingFrontScale.y * Mathf.Lerp(0.25f, 1f, f), wingFrontScale.z);
        if (wingBack != null) wingBack.localScale = new Vector3(wingBackScale.x, wingBackScale.y * Mathf.Lerp(0.25f, 1f, 1f - f), wingBackScale.z);

        if (Time.timeScale <= 0f) return;
        float tilt = Mathf.Clamp(vy * 4f, -maxTilt, maxTilt);
        body.localRotation = Quaternion.Lerp(body.localRotation, Quaternion.Euler(0, 0, tilt), 10f * Time.deltaTime);

        bump = Mathf.MoveTowards(bump, 0f, 4f * Time.deltaTime);
        float squash = 1f + Mathf.Sin(bump * Mathf.PI) * 0.18f;
        body.localScale = new Vector3(bodyScale.x * squash, bodyScale.y / squash, bodyScale.z);
    }
}
