using UnityEngine;

public class FloatingUI : MonoBehaviour
{
    [Header("Floating")]
    public float floatHeight = 15f;
    public float floatSpeed = 1f;

    [Header("Rotation")]
    public float rotationAmount = 3f;
    public float rotationSpeed = 0.8f;

    [Header("Scale")]
    public float scaleAmount = 0.02f;
    public float scaleSpeed = 0.8f;

    private RectTransform rectTransform;

    private Vector2 startPosition;
    private Vector3 startScale;
    private float startRotation;

    private float randomOffset;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();

        startPosition = rectTransform.anchoredPosition;
        startScale = rectTransform.localScale;
        startRotation = rectTransform.localEulerAngles.z;

        // ทำให้แต่ละ element เริ่ม animation ไม่พร้อมกัน
        randomOffset = Random.Range(0f, 10f);
    }

    void Update()
    {
        float time = Time.time + randomOffset;

        // =========================
        // Floating
        // =========================

        float y = Mathf.Sin(time * floatSpeed) * floatHeight;

        rectTransform.anchoredPosition =
            startPosition + new Vector2(0f, y);


        // =========================
        // Rotation
        // =========================

        float rotation =
            Mathf.Sin(time * rotationSpeed) * rotationAmount;

        rectTransform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                startRotation + rotation
            );


        // =========================
        // Scale
        // =========================

        float scale =
            1f + Mathf.Sin(time * scaleSpeed) * scaleAmount;

        rectTransform.localScale =
            startScale * scale;
    }
}