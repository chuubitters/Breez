using System;
using System.Text;
using TMPro;
using UnityEngine;

[Serializable] public class SummaryMode { public string mode; public int count; public int best_score; }
[Serializable] public class SummaryMoodCount { public string mood; public int count; }
[Serializable] public class SummaryMood { public string mood; public string note; public string recorded_at; }
[Serializable]
public class SummarySession
{
    public string mode;
    public int score;
    public int duration;
    public string played_at;
    // -1 = ไม่มีค่า (ไม่ใช่โหมดนั้น)
    public int flame_out_count = -1;
    public int balloons_total = -1;
    public int balloons_popped = -1;
    public int obstacles_avoided = -1;
}

[Serializable] public class SummaryModeCounts { public int calm; public int power; public int focus; public int soundscape; public int quick; }

[Serializable]
public class PlayerSummary
{
    public string name;
    public int total_sessions;
    public float total_minutes;
    public int best_score;
    public int streak_days;
    public string today_mood;   // อารมณ์ที่บันทึกวันนี้ ("" = ยังไม่บันทึก)
    public string top_mood;     // อารมณ์ที่พบบ่อยที่สุดในช่วงเวลา
    public SummaryModeCounts mode_counts;
    public SummaryMode[] modes;
    public SummaryMoodCount[] mood_counts;
    public SummaryMood[] recent_moods;
    public SummarySession[] recent_sessions;
}

// =====================================================================
// หน้า Mood Tracking & Summary ในเกม: ดึงสรุปของผู้เล่นเองมาแสดง
// ใส่ TMP_Text ที่ต้องการ ช่องไหนไม่ใช้ก็ปล่อยว่างได้
// =====================================================================
public class PlayerSummaryUI : MonoBehaviour
{
    [Tooltip("ย้อนหลังกี่วัน (0 = ทั้งหมด)")]
    public int days = 30;

    public TMP_Text nameText;
    public TMP_Text sessionsText;
    public TMP_Text minutesText;
    public TMP_Text bestScoreText;
    public TMP_Text streakText;
    public TMP_Text modesText;
    public TMP_Text moodsText;
    public TMP_Text recentText;
    public TMP_Text statusText;

    public PlayerSummary Data { get; private set; }
    public event Action<PlayerSummary> Loaded;

    void OnEnable() => Refresh();

    public void Refresh()
    {
        if (!BreezBackend.Instance.IsLoggedIn) { SetStatus("Please sign in"); return; }
        SetStatus("Loading...");

        string body = Json.Obj("p_days", days > 0 ? Json.Num(days) : "null");
        BreezBackend.Instance.Rpc("my_summary", body, (ok, text) =>
        {
            if (!ok) { SetStatus(text); return; }
            try { Data = JsonUtility.FromJson<PlayerSummary>(text); }
            catch (Exception e) { SetStatus("Could not read summary"); Debug.LogWarning(e); return; }
            SetStatus("");
            Render();
            Loaded?.Invoke(Data);
        });
    }

    void Render()
    {
        var d = Data;
        Set(nameText, d.name);
        Set(sessionsText, d.total_sessions.ToString());
        Set(minutesText, d.total_minutes.ToString("0.#") + " min");
        Set(bestScoreText, d.best_score.ToString());
        Set(streakText, d.streak_days + (d.streak_days == 1 ? " day" : " days"));

        var sb = new StringBuilder();
        if (d.modes != null) foreach (var m in d.modes) sb.AppendLine($"{m.mode}: {m.count}x (best {m.best_score})");
        Set(modesText, sb.Length > 0 ? sb.ToString() : "No games yet");

        sb.Clear();
        if (d.mood_counts != null) foreach (var m in d.mood_counts) sb.AppendLine($"{m.mood}: {m.count}");
        Set(moodsText, sb.Length > 0 ? sb.ToString() : "No moods yet");

        sb.Clear();
        if (d.recent_sessions != null)
            foreach (var s in d.recent_sessions)
            {
                string when = DateTime.TryParse(s.played_at, out var t) ? t.ToLocalTime().ToString("dd MMM HH:mm") : "";
                sb.AppendLine($"{when}  {s.mode}  {s.score}  ({s.duration / 60}:{s.duration % 60:00})");
            }
        Set(recentText, sb.ToString());
    }

    void Set(TMP_Text t, string v) { if (t != null) t.text = v; }
    void SetStatus(string v) { if (statusText != null) statusText.text = v; }
}
