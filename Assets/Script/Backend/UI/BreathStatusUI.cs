using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// ป้าย "status : หายใจเข้า / หายใจออก" ใช้ได้ทุกหน้าเกม
// - เป่า (ค่าจาก ESP32 >= threshold) หรือกดลูกศรขึ้น = หายใจออก
// - ไม่เป่า = หายใจเข้า
// ลาก prefab Assets/Prefabs/Breez/BreathStatus.prefab ใส่ Canvas ได้เลย
// (สร้างด้วยเมนู Breez > Create Status Elements)
// =====================================================================
public class BreathStatusUI : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("ไม่ใส่ก็ได้ จะหาใน scene ให้เอง")]
    public BreathReceiver breathReceiver;
    [Range(0, 100)] public int threshold = 50;
    [Tooltip("ต้องต่ำกว่า threshold เท่านี้ถึงจะกลับเป็นหายใจเข้า (กันกระพริบ)")]
    [Range(0, 50)] public int hysteresis = 10;
    [Tooltip("สถานะต้องคงที่นานเท่านี้ (วินาที) ก่อนเปลี่ยนข้อความ")]
    public float holdTime = 0.12f;
    public bool arrowKeyAlsoExhales = true;

    [Header("Text")]
    public TMP_Text label;
    public string prefix = "status : ";
    public string inhaleText = "หายใจเข้า";
    public string exhaleText = "หายใจออก";

    [Header("Look (ไม่บังคับ)")]
    public Image background;
    public Color inhaleColor = Color.white;
    public Color exhaleColor = new Color32(232, 243, 250, 255);
    [Tooltip("แถบแสดงแรงเป่า (Image แบบ Filled) ไม่ใส่ก็ได้")]
    public Image levelFill;
    public float pulseScale = 1.05f;

    /// <summary>true = กำลังหายใจออก (เป่าอยู่)</summary>
    public bool IsExhaling { get; private set; }
    public int Level => breathReceiver != null ? breathReceiver.breathLevel : 0;

    bool pending;
    float pendingSince;
    float pulse;
    float shownLevel;

    void Awake()
    {
        if (breathReceiver == null) breathReceiver = FindObjectOfType<BreathReceiver>();
        Render();
    }

    void Update()
    {
        if (breathReceiver == null) breathReceiver = FindObjectOfType<BreathReceiver>();

        int level = Level;
        bool arrow = arrowKeyAlsoExhales && Input.GetKey(KeyCode.UpArrow);
        bool raw = arrow || (IsExhaling ? level >= threshold - hysteresis : level >= threshold);

        if (raw != IsExhaling)
        {
            if (!pending) { pending = true; pendingSince = Time.unscaledTime; }
            if (Time.unscaledTime - pendingSince >= holdTime)
            {
                IsExhaling = raw;
                pending = false;
                pulse = 1f;
                Render();
            }
        }
        else pending = false;

        // แอนิเมชันเล็กๆ ตอนเปลี่ยนสถานะ + แถบแรงเป่า
        if (pulse > 0f)
        {
            pulse = Mathf.Max(0f, pulse - Time.unscaledDeltaTime * 4f);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, pulseScale, Mathf.Sin(pulse * Mathf.PI));
        }
        if (levelFill != null)
        {
            float target = arrow ? 1f : level / 100f;
            shownLevel = Mathf.Lerp(shownLevel, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));
            levelFill.fillAmount = shownLevel;
        }
    }

    void Render()
    {
        if (label != null) label.text = prefix + "<b>" + (IsExhaling ? exhaleText : inhaleText) + "</b>";
        if (background != null) background.color = IsExhaling ? exhaleColor : inhaleColor;
    }
}
