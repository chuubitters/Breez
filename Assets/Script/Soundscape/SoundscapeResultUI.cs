using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// Soundscape: หน้าผลลัพธ์หลังกด "จบการฝึก"
//   แสดงเวลาที่ใช้ในโหมดนี้ + จำนวนรอบหายใจ + เวลาที่เป่า
// SPGameManager เรียก Show(...)  สร้างด้วยเมนู Breez > Restyle Soundscape Countdown
// =====================================================================
public class SoundscapeResultUI : MonoBehaviour
{
    public CanvasGroup group;
    public RectTransform card;
    public TMP_Text timeText;
    public TMP_Text cyclesText;
    public TMP_Text exhaleText;
    public TMP_Text noteText;
    public string minuteUnit = "นาที";
    public string cycleUnit = "รอบ";
    public string secondUnit = "วินาที";

    float t = 1f;

    public void Show(float seconds, int cycles, float exhaleSeconds, bool saved)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        if (timeText != null)
            timeText.text = string.Format("{0:00}:{1:00}<size=40%>  {2}</size>", total / 60, total % 60, minuteUnit);
        if (cyclesText != null)
            cyclesText.text = cycles + "<size=40%>  " + cycleUnit + "</size>";
        if (exhaleText != null)
            exhaleText.text = Mathf.RoundToInt(exhaleSeconds) + "<size=40%>  " + secondUnit + "</size>";
        if (noteText != null)
            noteText.text = saved ? "บันทึกลงประวัติการเล่นแล้ว" : "ฝึกสั้นเกินไป จึงยังไม่บันทึกลงประวัติ";
        t = 0f;
        Apply();
    }

    void Update()
    {
        if (t >= 1f) return;
        t = Mathf.Min(1f, t + Time.unscaledDeltaTime * 3f);
        Apply();
    }

    void Apply()
    {
        float k = 1f - Mathf.Pow(1f - t, 3f);
        if (group != null) { group.alpha = k; group.blocksRaycasts = true; group.interactable = true; }
        if (card != null) card.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, k);
    }
}
