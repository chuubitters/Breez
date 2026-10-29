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
    [Tooltip("ใช้ข้อความภาษาไทย (ต้องมี font ที่รองรับภาษาไทย)")]
    public bool thaiText;
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

    PlayerSummary data;
    bool saving;

    void Start()
    {
        if (backButton != null) backButton.onClick.AddListener(GoBack);
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

    public void Refresh()
    {
        if (!BreezBackend.Instance.IsLoggedIn) { SetStatus(thaiText ? "กรุณาเข้าสู่ระบบ" : "Please sign in"); return; }
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
        SetStatus(thaiText ? "กำลังบันทึก..." : "Saving...");

        GameDataRecorder.LogMood(mood, null, ok =>
        {
            saving = false;
            if (this == null) return;
            SetStatus(ok
                ? (thaiText ? "บันทึกอารมณ์แล้ว" : "Mood saved")
                : (thaiText ? "บันทึกแบบออฟไลน์ จะส่งเมื่อออนไลน์" : "Saved offline, will sync later"));
            Refresh();
        });
    }

    // -----------------------------------------------------------------

    void Render()
    {
        var d = data;
        if (d == null) return;

        HighlightMood(string.IsNullOrEmpty(d.today_mood) ? null : d.today_mood);

        Set(sessionsText, Big(d.total_sessions.ToString(), thaiText ? "ครั้ง" : "Sessions"));

        // อารมณ์ที่พบบ่อยที่สุด
        bool hasMood = !string.IsNullOrEmpty(d.top_mood);
        if (topMoodIcon != null)
        {
            topMoodIcon.enabled = hasMood;
            if (hasMood) topMoodIcon.sprite = IconFor(d.top_mood);
        }
        Set(topMoodText, hasMood ? d.top_mood : (thaiText ? "ยังไม่มีข้อมูล" : "No moods yet"));

        var c = d.mode_counts ?? new SummaryModeCounts();
        Set(calmCountText, Big(c.calm.ToString(), "Calm Flow", 20));
        Set(powerCountText, Big(c.power.ToString(), "Power Release", 20));
        Set(focusCountText, Big(c.focus.ToString(), "Breathe & Focus", 20));

        Set(streakText, Big(d.streak_days.ToString(), thaiText ? "วัน" : (d.streak_days == 1 ? "Day" : "Days"), 26, 96));

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

    string Detail(SummarySession s)
    {
        string time = $"{s.duration / 60:00}:{s.duration % 60:00}";
        string line1 = thaiText ? $"ระยะเวลาที่ใช้ {time} นาที" : $"Time played {time} min";
        string line2 = "";
        if (s.obstacles_avoided >= 0)
            line2 = thaiText ? $"ข้ามสิ่งกีดขวางได้ทั้งหมด {s.obstacles_avoided} ชิ้น" : $"Obstacles avoided: {s.obstacles_avoided}";
        else if (s.balloons_total >= 0)
            line2 = thaiText ? $"ลูกโป่งที่เป่าได้ {s.balloons_popped}/{s.balloons_total}" : $"Balloons popped: {s.balloons_popped}/{s.balloons_total}";
        else if (s.mode == GameDataRecorder.CalmFlow && s.flowers_grown > 0)
            line2 = thaiText ? $"ปลูกดอกไม้ {s.flowers_grown} ดอก · ใหญ่สุด {s.largest_flower_pct}%" : $"Flowers grown: {s.flowers_grown} · largest {s.largest_flower_pct}%";
        else if (s.flame_out_count >= 0)
            line2 = thaiText ? $"เปลวไฟดับ {s.flame_out_count} ครั้ง" : $"Flame went out: {s.flame_out_count}x";
        else if (s.mode == GameDataRecorder.Soundscape)
            line2 = thaiText ? "ฝึกหายใจ เข้า 5 วิ ออก 5 วิ" : "Breathing 5s in / 5s out";
        else if (s.mode == GameDataRecorder.QuickSession)
            line2 = thaiText ? "ฝึกหายใจแบบ 4-7-8" : "4-7-8 breathing";
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
