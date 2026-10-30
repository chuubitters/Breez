using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// Soundscape: นับถอยหลังก่อนเริ่ม (3 · 2 · 1 · เริ่ม!)
//   วงกลมสีขาวกลางจอ + วงแหวนลดลงทีละวินาที + ตัวเลขเด้งเข้า
// SPGameManager เรียก Show / Tick / Go / Hide เอง
// สร้างด้วยเมนู Breez > Restyle Soundscape Countdown
// =====================================================================
public class SoundscapeCountdownUI : MonoBehaviour
{
    public CanvasGroup group;
    public RectTransform bubble;
    public Image ring;
    public TMP_Text number;
    public TMP_Text caption;
    public string captionText = "เตรียมตัว หายใจตามจังหวะ";
    public string goText = "เริ่ม!";
    public float numberSize = 150f;
    public float goSize = 110f;
    [Tooltip("พื้นหลังสีขาวตอนนับถอยหลัง (ตัวลูกชื่อ Blocker)")]
    public Color backdropColor = new Color(1f, 1f, 1f, 0.94f);
    [Tooltip("สีวงแหวนพื้น (ตัวลูกชื่อ RingTrack) ให้เห็นชัดบนพื้นขาว")]
    public Color trackColor = new Color32(0xE4, 0xEC, 0xDD, 255);

    float popT = 1f, ringFrom = 1f, ringTo = 1f, ringT = 1f, ringDur = 1f;
    float fadeTarget, fade;

    void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        // พื้นหลังขาว + วงแหวนพื้นสีเขียวอ่อน (ใช้ได้ทันทีโดยไม่ต้องกดเมนูใหม่)
        var blocker = transform.Find("Blocker");
        if (blocker != null) { var img = blocker.GetComponent<Image>(); if (img != null) { img.color = backdropColor; img.raycastTarget = true; } }
        foreach (var img in GetComponentsInChildren<Image>(true))
            if (img.name == "RingTrack") img.color = trackColor;
        transform.SetAsLastSibling();
        fade = fadeTarget = 0f;
        Apply();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        fadeTarget = 1f;
        if (caption != null) caption.text = captionText;
    }

    /// <summary>แสดงตัวเลข และให้วงแหวนลดจาก from ไป to ใน duration วินาที</summary>
    public void Tick(int value, float from, float to, float duration)
    {
        if (number != null) { number.text = value.ToString(); number.fontSize = numberSize; }
        popT = 0f;
        ringFrom = from; ringTo = to; ringT = 0f; ringDur = Mathf.Max(0.05f, duration);
    }

    public void Go()
    {
        if (number != null) { number.text = goText; number.fontSize = goSize; }
        popT = 0f;
        ringFrom = ringTo = 0f; ringT = 1f;
    }

    public void Hide() { fadeTarget = 0f; }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        popT = Mathf.Min(1f, popT + dt * 3.2f);
        ringT = Mathf.Min(1f, ringT + dt / ringDur);
        fade = Mathf.MoveTowards(fade, fadeTarget, dt * 4f);
        Apply();
        if (fade <= 0f && fadeTarget <= 0f && gameObject.activeSelf) gameObject.SetActive(false);
    }

    void Apply()
    {
        if (group != null) { group.alpha = fade; group.blocksRaycasts = fade > 0.5f; }
        // ตัวเลขเด้ง: ใหญ่ 1.35 -> 1 (ease out back)
        float k = 1f - Mathf.Pow(1f - popT, 3f);
        float overshoot = Mathf.Sin(popT * Mathf.PI) * 0.08f;
        if (number != null)
        {
            number.rectTransform.localScale = Vector3.one * (Mathf.Lerp(1.35f, 1f, k) + overshoot);
            var c = number.color; c.a = Mathf.Clamp01(popT * 2.5f); number.color = c;
        }
        if (bubble != null) bubble.localScale = Vector3.one * (0.9f + 0.1f * fade) * (1f + overshoot * 0.3f);
        if (ring != null) ring.fillAmount = Mathf.Lerp(ringFrom, ringTo, Mathf.SmoothStep(0f, 1f, ringT));
    }
}
