using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// Soundscape: ป้ายนำการหายใจ (หายใจเข้า 5 วิ / หายใจออก 5 วิ)
//   [วงกลมหายใจ ขยาย/หด]  หายใจเข้า        4
//                         ▓▓▓▓▓░░░░░    วินาที
// SPGameManager เรียก SetPhase ทุกเฟรม
// สร้างด้วยเมนู Breez > Restyle Soundscape Countdown
// =====================================================================
public class BreathGuideUI : MonoBehaviour
{
    public CanvasGroup group;
    public TMP_Text phaseText;
    public TMP_Text secondsText;
    public TMP_Text unitText;
    public Image progressFill;
    public RectTransform breathCircle;
    public Image breathCircleImage;

    public string inhaleLabel = "สูดลมหายใจเข้า";
    public string exhaleLabel = "ผ่อนลมหายใจออก";
    [Tooltip("วงแหวน: ลดลงตามเวลาที่เหลือ (ปิด = เต็มขึ้นตามความคืบหน้า)")]
    public bool ringShowsRemaining = true;
    [Tooltip("ย้อมสีวงแหวนตามเฟส (ปิด = ใช้สีไล่ของรูปวงแหวน)")]
    public bool tintProgress = false;
    public string unitLabel = "วินาที";
    public Color inhaleColor = new Color32(0xB9, 0xDA, 0xEC, 255);
    public Color exhaleColor = new Color32(0xD4, 0xE5, 0x9D, 255);
    [Range(0.2f, 1f)] public float circleMin = 0.55f;

    float show;

    void Awake() { if (group == null) group = GetComponent<CanvasGroup>(); if (group != null) group.alpha = 0f; }

    /// <summary>inhale = หายใจเข้า, secondsLeft = วินาทีที่เหลือ, t = ความคืบหน้าของเฟสนี้ 0-1</summary>
    public void SetPhase(bool inhale, int secondsLeft, float t)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        show = 1f;
        if (phaseText != null) phaseText.text = inhale ? inhaleLabel : exhaleLabel;
        if (secondsText != null) secondsText.text = secondsLeft.ToString();
        if (unitText != null) unitText.text = unitLabel;

        var col = inhale ? inhaleColor : exhaleColor;
        if (progressFill != null)
        {
            float k = Mathf.Clamp01(t);
            progressFill.fillAmount = ringShowsRemaining ? 1f - k : k;
            if (tintProgress) progressFill.color = col;
        }

        // วงกลม: หายใจเข้า = ค่อยๆ ขยาย, หายใจออก = ค่อยๆ หด
        float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
        float s = inhale ? Mathf.Lerp(circleMin, 1f, e) : Mathf.Lerp(1f, circleMin, e);
        if (breathCircle != null) breathCircle.localScale = Vector3.one * s;
        if (breathCircleImage != null) breathCircleImage.color = col;
    }

    void Update()
    {
        if (group == null) return;
        group.alpha = Mathf.MoveTowards(group.alpha, show, Time.unscaledDeltaTime * 3f);
    }
}
