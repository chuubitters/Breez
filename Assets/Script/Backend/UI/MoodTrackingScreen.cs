using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================
// หน้า Mood Tracking & Weekly Summary
// สร้างพร้อมต่อสายด้วยเมนู Breez > Create Mood Tracking Scene
// - เลือกอารมณ์วันนี้ (บันทึกลง mood_logs)
// - สรุป 7 วัน: จำนวน session, อารมณ์ที่พบบ่อย, จำนวนแต่ละเกม, streak, การเล่นล่าสุด
// =====================================================================
public class MoodTrackingScreen : MonoBehaviour
{
    [Serializable]
    public class MoodButton
    {
        public string mood;      // Lovely / Happy / Bored / Angry / Stress
        public Button button;
        public Image frame;      // กรอบที่เปลี่ยนสีตอนเลือก
        public Image icon;
    }

    [Serializable]
    public class RecentRow
    {
        public GameObject root;
        public TMP_Text title;
        public TMP_Text detail;
        public TMP_Text score;
    }

    [Header("Settings")]
    public int days = 7;
    public string backScene = "Mode";

    [Header("Top")]
    public Button backButton;
    public TMP_Text dateText;
    public TMP_Text statusText;

    [Header("Mood of the day")]
    public MoodButton[] moodButtons;
    public Color frameNormal = new Color32(226, 228, 220, 255);
    public Color frameSelected = new Color32(166, 203, 69, 255);

    [Header("Summary")]
    public TMP_Text sessionsText;
    public Image topMoodIcon;
    public TMP_Text topMoodText;
    public TMP_Text calmCountText;
    public TMP_Text powerCountText;
    public TMP_Text focusCountText;
    public TMP_Text streakText;

    [Header("Recent play")]
    public RecentRow[] recentRows;
    public TMP_Text emptyRecentText;

    [Header("History (ดูประวัติวันอื่น)")]
    public Button historyButton;
    public DailyHistoryPanel historyPanel;

    PlayerSummary data;
    bool saving;

    void Start()
    {
        if (backButton != null) backButton.onClick.AddListener(GoBack);
        if (historyButton != null) historyButton.onClick.AddListener(OpenHistory);
        if (moodButtons != null)
            foreach (var mb in moodButtons)
            {
                var captured = mb.mood;
                if (mb.button != null) mb.button.onClick.AddListener(() => SelectMood(captured));
            }

        if (dateText != null) dateText.text = DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        HighlightMood(null);
        Refresh();
    }

    public void GoBack()
    {
        if (!string.IsNullOrEmpty(backScene)) SceneManager.LoadScene(backScene);
    }

    public void OpenHistory()
    {
        if (historyPanel != null) historyPanel.Open(DateTime.Now.Date);
    }

    public void Refresh()
    {
        if (!BreezBackend.Instance.IsLoggedIn) { SetStatus("กรุณาเข้าสู่ระบบ"); return; }
        SetStatus("");

        string body = Json.Obj("p_days", days > 0 ? Json.Num(days) : "null");
        BreezBackend.Instance.Rpc("my_summary", body, (ok, text) =>
        {
            if (!ok) { SetStatus(text); return; }
            try { data = JsonUtility.FromJson<PlayerSummary>(text); }
            catch (Exception e) { Debug.LogWarning("[Breez] Summary parse error: " + e.Message); return; }
            if (this == null) return; // ออกจาก scene ไปแล้ว
            Render();
        });
    }

    public void SelectMood(string mood)
    {
        if (saving) return;
        saving = true;
        HighlightMood(mood);
        SetStatus("กำลังบันทึก...");

        GameDataRecorder.LogMood(mood, null, ok =>
        {
            saving = false;
            if (this == null) return;
            SetStatus(ok
                ? "บันทึกอารมณ์แล้ว"
                : "บันทึกแบบออฟไลน์ จะส่งเมื่อออนไลน์");
            Refresh();
        });
    }

    // -----------------------------------------------------------------

    void Render()
    {
        var d = data;
        if (d == null) return;

        HighlightMood(string.IsNullOrEmpty(d.today_mood) ? null : d.today_mood);

        Set(sessionsText, Big(d.total_sessions.ToString(), "ครั้ง"));

        // อารมณ์ที่พบบ่อยที่สุด
        bool hasMood = !string.IsNullOrEmpty(d.top_mood);
        if (topMoodIcon != null)
        {
            topMoodIcon.enabled = hasMood;
            if (hasMood) topMoodIcon.sprite = IconFor(d.top_mood);
        }
        Set(topMoodText, hasMood ? BreezText.Mood(d.top_mood) : "ยังไม่มีข้อมูล");

        var c = d.mode_counts ?? new SummaryModeCounts();
        Set(calmCountText, Big(c.calm.ToString(), "Calm Flow", 20));
        Set(powerCountText, Big(c.power.ToString(), "Power Release", 20));
        Set(focusCountText, Big(c.focus.ToString(), "Breathe & Focus", 20));

        Set(streakText, Big(d.streak_days.ToString(), "วัน", 26, 96));

        // การเล่นล่าสุด
        int n = d.recent_sessions != null ? d.recent_sessions.Length : 0;
        if (recentRows != null)
            for (int i = 0; i < recentRows.Length; i++)
            {
                var row = recentRows[i];
                bool show = i < n;
                if (row.root != null) row.root.SetActive(show);
                if (!show) continue;

                var s = d.recent_sessions[i];
                Set(row.title, s.mode);
                Set(row.detail, Detail(s));
                Set(row.score, s.score > 0 ? s.score.ToString() : "—");
            }
        if (emptyRecentText != null) emptyRecentText.gameObject.SetActive(n == 0);
    }

    public static string Detail(SummarySession s)
    {
        string line1 = $"ระยะเวลาที่ใช้ {BreezText.Clock(s.duration)} นาที";
        string line2 = BreezText.SessionResult(s);
        return string.IsNullOrEmpty(line2) ? line1 : line1 + "\n" + line2;
    }

    void HighlightMood(string mood)
    {
        if (moodButtons == null) return;
        foreach (var mb in moodButtons)
        {
            if (mb.frame == null) continue;
            bool selected = mood != null && string.Equals(mb.mood, mood, StringComparison.OrdinalIgnoreCase);
            mb.frame.color = selected ? frameSelected : frameNormal;
            if (mb.button != null) mb.button.transform.localScale = selected ? Vector3.one * 1.06f : Vector3.one;
        }
    }

    Sprite IconFor(string mood)
    {
        if (moodButtons != null)
            foreach (var mb in moodButtons)
                if (string.Equals(mb.mood, mood, StringComparison.OrdinalIgnoreCase) && mb.icon != null)
                    return mb.icon.sprite;
        return null;
    }

    static string Big(string number, string unit, int unitSize = 22, int numberSize = 80) =>
        $"<size={numberSize}>{number}</size>  <size={unitSize}>{unit}</size>";

    static void Set(TMP_Text t, string v) { if (t != null) t.text = v; }
    void SetStatus(string v) { if (statusText != null) statusText.text = v; }
}
