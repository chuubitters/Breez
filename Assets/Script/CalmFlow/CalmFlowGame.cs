using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// Calm Flow: ผ่อนลมหายใจออกช้าๆ ต่อเนื่อง ให้ดอกไม้ค่อยๆ บาน
//   1) เลือกเวลา Session 1 / 2 / 3 นาที -> Start Session
//   2) เป่า (ESP32 >= threshold หรือกดลูกศรขึ้น) = ดอกไม้โตตามเวลาที่เป่าต่อเนื่อง
//   3) หยุดเป่านานกว่าช่วงผ่อนผัน (graceSeconds) = บันทึกขนาดดอก ปลูกลงสวน แล้วเริ่มดอกใหม่
//      (สัญญาณตกแป๊บเดียวจากเสียงรบกวน ไม่นับว่าหยุดเป่า)
//   4) หมดเวลา = หยุดรับ input แล้วสรุปผล + บันทึกขึ้น Supabase
// เวลา Session คือเวลารวม ไม่ได้บังคับให้เป่าตลอด หายใจตามสบาย
// =====================================================================
public class CalmFlowGame : MonoBehaviour
{
    public enum State { Select, Playing, Paused, Done }

    [Header("Breath")]
    public BreathReceiver breathReceiver;
    [Range(0, 100)] public int breathThreshold = 50;
    public bool arrowKeyBlows = true;
    [Tooltip("สัญญาณต่ำกว่า threshold ได้นานเท่านี้ (วินาที) ก่อนนับว่าหยุดเป่า (แนะนำ 0.4-0.7)")]
    [Range(0.1f, 2f)] public float graceSeconds = 0.55f;
    [Tooltip("เป่าต่อเนื่องกี่วินาทีดอกถึงจะบานเต็มที่ (100%)")]
    public float secondsToFullBloom = 8f;
    [Tooltip("เป่าสั้นกว่านี้ไม่นับเป็นดอก (กันเสียงรบกวน)")]
    public float minExhaleSeconds = 0.6f;

    [Header("Session")]
    public int[] durationsMinutes = { 1, 2, 3 };
    public int selectedIndex = 1;

    [Header("Flower")]
    public BloomFlower flower;
    [Tooltip("ที่วางดอกไม้ที่ปลูกเสร็จแล้ว (แถวสวนด้านล่าง)")]
    public RectTransform garden;
    public float gardenScale = 0.3f;
    public float gardenSpacing = 112f;
    public int gardenPerRow = 15;

    [Header("วิธีการเล่น (ไม่ใส่ = ข้าม)")]
    public GameObject howToPanel;
    public Button howToStartButton;

    [Header("Select Screen")]
    public GameObject selectPanel;
    public Button[] durationButtons;
    public Color durationOn = new Color32(0xF2, 0xBD, 0xC9, 255);
    public Color durationOff = Color.white;
    public Color durationTextOn = Color.white;
    public Color durationTextOff = new Color32(0x55, 0x55, 0x55, 255);
    public TextMeshProUGUI readyTimerText;

    [Header("HUD")]
    public GameObject hud;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI hintText;
    public TextMeshProUGUI bloomText;
    public TextMeshProUGUI flowerCountText;
    public string hintIdle = "ค่อยๆ ผ่อนลมหายใจออก";
    public string hintBlowing = "ดีมาก ผ่อนลมต่อไปช้าๆ";
    public string hintBloomed = "บานเต็มที่แล้ว หยุดพักหายใจเข้าได้เลย";
    public string flowerCountFormat = "ดอกไม้ {0}";

    [Header("Pause")]
    public GameObject pausePanel;
    public TextMeshProUGUI pauseButtonText;

    [Header("Summary")]
    public GameObject summaryPanel;
    public TextMeshProUGUI summarySubtitle;
    public TextMeshProUGUI flowersGrownText;
    public TextMeshProUGUI largestFlowerText;
    public TextMeshProUGUI longestExhaleText;
    public TextMeshProUGUI totalExhaleText;
    public string subtitleFormat = "ฝึกครบ {0} นาทีแล้ว";

    // ---------------- ผล ----------------
    public State Current { get; private set; } = State.Select;
    public int FlowersGrown { get; private set; }
    public float LargestFlower01 { get; private set; }
    public float LongestExhale { get; private set; }
    public float TotalExhale { get; private set; }
    public float TimeLeft { get; private set; }

    float sessionSeconds, played;
    bool growing;
    float exhaleSpan, exhaleAbove, belowFor, sumBloom;
    readonly List<GameObject> planted = new List<GameObject>();

    void Awake()
    {
        if (breathReceiver == null) breathReceiver = FindObjectOfType<BreathReceiver>();
    }

    void Start()
    {
        SelectDuration(selectedIndex);
        ShowOnly(selectPanel);
        if (flower != null) flower.SetInstant(0f);
        if (howToPanel != null)
        {
            howToPanel.SetActive(true);
            if (howToStartButton != null) howToStartButton.onClick.AddListener(CloseHowTo);
        }
    }

    /// <summary>ปิดหน้าวิธีการเล่น แล้วไปเลือกเวลา</summary>
    public void CloseHowTo()
    {
        if (howToPanel != null) howToPanel.SetActive(false);
    }

    // ---------------------------------------------------------------
    // ปุ่ม
    // ---------------------------------------------------------------
    public void SelectDuration(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, durationsMinutes.Length - 1);
        sessionSeconds = durationsMinutes[selectedIndex] * 60f;
        if (readyTimerText != null) readyTimerText.text = Format(sessionSeconds);
        if (durationButtons == null) return;
        for (int i = 0; i < durationButtons.Length; i++)
        {
            var b = durationButtons[i];
            if (b == null) continue;
            bool on = i == selectedIndex;
            var img = b.GetComponent<Image>();
            if (img != null) img.color = on ? durationOn : durationOff;
            var t = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t != null) t.color = on ? durationTextOn : durationTextOff;
        }
    }

    public void StartSession()
    {
        BreezAudio.Play("count_go", 0.7f);
        sessionSeconds = durationsMinutes[selectedIndex] * 60f;
        TimeLeft = sessionSeconds;
        played = 0f;
        FlowersGrown = 0; LargestFlower01 = 0f; LongestExhale = 0f; TotalExhale = 0f; sumBloom = 0f;
        growing = false; exhaleSpan = exhaleAbove = belowFor = 0f;
        foreach (var go in planted) if (go != null) Destroy(go);
        planted.Clear();
        if (flower != null) { flower.SetInstant(0f); flower.breathing = false; }
        Current = State.Playing;
        ShowOnly(hud);
        UpdateHud();
    }

    public void TogglePause()
    {
        if (Current == State.Playing) Pause();
        else if (Current == State.Paused) Resume();
    }

    public void Pause()
    {
        if (Current != State.Playing) return;
        Current = State.Paused;
        if (pausePanel != null) pausePanel.SetActive(true);
        if (pauseButtonText != null) pauseButtonText.text = "เล่นต่อ";
        if (flower != null) flower.breathing = false;
    }

    public void Resume()
    {
        if (Current != State.Paused) return;
        Current = State.Playing;
        belowFor = 0f;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseButtonText != null) pauseButtonText.text = "พัก";
    }

    /// <summary>จบก่อนเวลา (ปุ่มใน Pause) สรุปผลเท่าที่เล่นไป</summary>
    public void FinishNow()
    {
        if (Current == State.Playing || Current == State.Paused) EndSession();
    }

    // ---------------------------------------------------------------
    void Update()
    {
        if (Current != State.Playing) return;
        float dt = Time.deltaTime;

        played += dt;
        TimeLeft = Mathf.Max(0f, sessionSeconds - played);

        int level = breathReceiver != null ? breathReceiver.breathLevel : 0;
        bool blow = level >= breathThreshold || (arrowKeyBlows && Input.GetKey(KeyCode.UpArrow));

        if (blow)
        {
            if (!growing) { growing = true; exhaleSpan = 0f; exhaleAbove = 0f; bloomSoundPlayed = false; }
            exhaleSpan += belowFor + dt;   // ช่วงตกสั้นๆ ระหว่างเป่า นับรวมเป็นการเป่าต่อเนื่อง
            exhaleAbove += dt;
            TotalExhale += dt;
            belowFor = 0f;
        }
        else if (growing)
        {
            belowFor += dt;
            if (belowFor > graceSeconds) FinishFlower();
        }

        if (flower != null)
        {
            flower.breathing = growing && belowFor <= 0f;
            if (growing) flower.progress = Mathf.Clamp01(exhaleSpan / Mathf.Max(0.1f, secondsToFullBloom));
            if (growing && !bloomSoundPlayed && flower.progress >= 1f) { bloomSoundPlayed = true; BreezAudio.Play("flower_bloom"); }
        }

        if (TimeLeft <= 0f) { EndSession(); return; }
        UpdateHud();
    }

    void FinishFlower()
    {
        float size = Mathf.Clamp01(exhaleSpan / Mathf.Max(0.1f, secondsToFullBloom));
        if (exhaleSpan >= minExhaleSeconds)
        {
            FlowersGrown++;
            BreezAudio.Play("flower_plant");
            sumBloom += size;
            LargestFlower01 = Mathf.Max(LargestFlower01, size);
            LongestExhale = Mathf.Max(LongestExhale, exhaleSpan);
            Plant(size);
        }
        growing = false;
        exhaleSpan = exhaleAbove = belowFor = 0f;
        if (flower != null) { flower.breathing = false; flower.progress = 0f; }   // หุบกลับเป็นต้นกล้า รอรอบใหม่
    }

    // ปลูกดอกที่เสร็จแล้วลงแถวสวน (สำเนาของดอกหลัก ย่อขนาดตามความใหญ่)
    void Plant(float size)
    {
        if (flower == null || garden == null) return;
        var copy = Instantiate(flower.gameObject, garden);
        copy.name = "Planted " + FlowersGrown;
        var bf = copy.GetComponent<BloomFlower>();
        bf.breathing = false;
        bf.SetInstant(size);
        var glow = bf.glow;
        if (glow != null) glow.gameObject.SetActive(false);

        int i = planted.Count;
        int row = i / gardenPerRow, col = i % gardenPerRow;
        float rowWidth = (gardenPerRow - 1) * gardenSpacing;
        var rt = (RectTransform)copy.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        float jitter = ((i * 37) % 11 - 5) * 4f;
        rt.anchoredPosition = new Vector2(-rowWidth / 2f + col * gardenSpacing + (row % 2) * gardenSpacing * 0.5f + jitter, row * 46f);
        rt.localScale = Vector3.one * gardenScale * Mathf.Lerp(0.65f, 1.1f, size) * (row > 0 ? 0.85f : 1f);
        if (row > 0) rt.SetAsFirstSibling();   // แถวหลังอยู่ด้านหลัง
        planted.Add(copy);
    }

    bool bloomSoundPlayed;

    void EndSession()
    {
        BreezAudio.Play("result_calm");
        if (growing) FinishFlower();
        Current = State.Done;
        if (flower != null) flower.breathing = false;

        int minutes = durationsMinutes[selectedIndex];
        int largestPct = Mathf.RoundToInt(LargestFlower01 * 100f);
        if (summarySubtitle != null) summarySubtitle.text = string.Format(subtitleFormat, minutes);
        if (flowersGrownText != null) flowersGrownText.text = FlowersGrown.ToString();
        if (largestFlowerText != null) largestFlowerText.text = largestPct + "%";
        if (longestExhaleText != null) longestExhaleText.text = LongestExhale.ToString("0.0") + " วิ";
        if (totalExhaleText != null) totalExhaleText.text = Mathf.RoundToInt(TotalExhale) + " วิ";
        ShowOnly(summaryPanel);
        if (hud != null) hud.SetActive(false);

        // คะแนน = ผลรวมขนาดดอก (%) / 10  เช่น 12 ดอก เฉลี่ย 70% = 84
        int score = Mathf.RoundToInt(sumBloom * 10f);
        GameDataRecorder.RecordCalmFlow(score, played, FlowersGrown, largestPct, LongestExhale, TotalExhale, minutes);
    }

    void UpdateHud()
    {
        if (timerText != null) timerText.text = Format(TimeLeft);
        float p = flower != null ? flower.progress : 0f;
        if (bloomText != null) bloomText.text = growing ? Mathf.RoundToInt(p * 100f) + "%" : "";
        if (flowerCountText != null) flowerCountText.text = string.Format(flowerCountFormat, FlowersGrown);
        if (hintText != null) hintText.text = !growing ? hintIdle : p >= 1f ? hintBloomed : hintBlowing;
    }

    void ShowOnly(GameObject panel)
    {
        if (selectPanel != null) selectPanel.SetActive(panel == selectPanel);
        if (hud != null) hud.SetActive(panel == hud);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (summaryPanel != null) summaryPanel.SetActive(panel == summaryPanel);
        if (pauseButtonText != null) pauseButtonText.text = "พัก";
    }

    public static string Format(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds - 0.0001f));
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }
}
