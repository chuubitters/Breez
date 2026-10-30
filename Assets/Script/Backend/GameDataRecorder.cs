using System;
using System.Globalization;
using UnityEngine;

// =====================================================================
// บันทึกผลการเล่นและอารมณ์ขึ้น Supabase
// เรียกจาก GameManager ของแต่ละเกมตอนจบเกม เช่น
//   GameDataRecorder.RecordPowerRelease(score, seconds, total, popped);
// ถ้าออฟไลน์ ข้อมูลจะถูกเก็บไว้และส่งให้อัตโนมัติภายหลัง
// =====================================================================
public static class GameDataRecorder
{
    public const string CalmFlow = "Calm Flow";
    public const string PowerRelease = "Power Release";
    public const string BreatheFocus = "Breathe & Focus";
    public const string Soundscape = "Soundscape";
    public const string QuickSession = "Quick Session";

    public static readonly string[] Moods = { "Lovely", "Happy", "Bored", "Angry", "Stress" };

    /// <summary>แจ้งผลการบันทึก (ok, ข้อความ) ใช้แสดงสถานะบน UI ได้</summary>
    public static event Action<bool, string> Saved;

    // ---------- ผลเกมแต่ละโหมด ----------

    /// <summary>Calm Flow (ดอกไม้บาน): จำนวนดอก, ดอกใหญ่สุด (%), เป่าต่อเนื่องนานสุด/รวม (วินาที), เวลาที่เลือก (นาที)</summary>
    public static void RecordCalmFlow(int score, float durationSeconds, int flowersGrown, int largestFlowerPct,
                                      float longestExhaleSeconds, float totalExhaleSeconds, int sessionMinutes, Action<bool> done = null) =>
        RecordSession(CalmFlow, score, durationSeconds, Json.Obj(
            "flowers_grown", Json.Num(flowersGrown),
            "largest_flower_pct", Json.Num(Mathf.Clamp(largestFlowerPct, 0, 100)),
            "longest_exhale_ms", Json.Num(Mathf.RoundToInt(longestExhaleSeconds * 1000f)),
            "total_exhale_ms", Json.Num(Mathf.RoundToInt(totalExhaleSeconds * 1000f)),
            "session_minutes", Json.Num(sessionMinutes)), done);

    public static void RecordPowerRelease(int score, float durationSeconds, int balloonsTotal, int balloonsPopped, Action<bool> done = null) =>
        RecordSession(PowerRelease, score, durationSeconds,
            Json.Obj("balloons_total", Json.Num(balloonsTotal), "balloons_popped", Json.Num(balloonsPopped)), done);

    public static void RecordBreatheFocus(int score, float durationSeconds, int obstaclesAvoided, Action<bool> done = null) =>
        RecordSession(BreatheFocus, score, durationSeconds, Json.Obj("obstacles_avoided", Json.Num(obstaclesAvoided)), done);

    /// <summary>Soundscape: ฉาก (Rainy Forest / Windy Mountain), จำนวนรอบหายใจ 5-5 ที่ครบ, เวลาที่เป่า (วินาที)</summary>
    public static void RecordSoundscape(float durationSeconds, string scene = null, int breathCycles = 0, float exhaleSeconds = 0f, Action<bool> done = null) =>
        RecordSession(Soundscape, 0, durationSeconds, Json.Obj(
            "scene", Json.Str(scene ?? ""),
            "breath_cycles", Json.Num(Mathf.Max(0, breathCycles)),
            "exhale_ms", Json.Num(Mathf.RoundToInt(Mathf.Max(0f, exhaleSeconds) * 1000f))), done);

    /// <summary>Quick Session 4-7-8 (ฝึกบนตัวเครื่อง) — rounds = รอบที่ครบ, blowPct = % เวลาที่เป่าได้ตอนหายใจออก</summary>
    public static void RecordQuickSession(float durationSeconds, int rounds = 0, int blowPct = -1, Action<bool> done = null) =>
        RecordSession(QuickSession, Mathf.Max(0, blowPct), durationSeconds, Json.Obj(
            "pattern", Json.Str("4-7-8"),
            "rounds", Json.Num(Mathf.Max(0, rounds)),
            "blow_pct", Json.Num(Mathf.Clamp(blowPct, 0, 100)),
            "source", Json.Str("gadget")), done);

    // ---------- ทั่วไป ----------

    public static void RecordSession(string mode, int score, float durationSeconds, string detailsJson = "{}", Action<bool> done = null)
    {
        var backend = BreezBackend.Instance;
        if (!backend.IsLoggedIn)
        {
            Debug.LogWarning($"[Breez] Not signed in — {mode} result was not saved.");
            done?.Invoke(false);
            return;
        }

        int seconds = Mathf.Max(0, Mathf.RoundToInt(durationSeconds));
        string startedAt = DateTime.UtcNow.AddSeconds(-seconds)
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        string deviceId = DeviceService.Current?.device_id;

        string body = Json.Obj(
            "p_mode", Json.Str(mode),
            "p_score", Json.Num(Mathf.Max(0, score)),
            "p_duration", Json.Num(seconds),
            "p_device_id", string.IsNullOrEmpty(deviceId) ? "null" : Json.Str(deviceId),
            "p_details", string.IsNullOrEmpty(detailsJson) ? "{}" : detailsJson,
            "p_played_at", Json.Str(startedAt));

        backend.Rpc("record_play_session", body, (ok, msg) =>
        {
            Debug.Log(ok ? $"[Breez] Saved {mode}: score {score}, {seconds}s" : $"[Breez] {mode}: {msg}");
            Saved?.Invoke(ok, ok ? "บันทึกผลแล้ว" : msg);
            done?.Invoke(ok);
        }, queueOnFailure: true);
    }

    /// <summary>mood = Lovely / Happy / Bored / Angry / Stress</summary>
    public static void LogMood(string mood, string note = null, Action<bool> done = null)
    {
        var backend = BreezBackend.Instance;
        if (!backend.IsLoggedIn) { done?.Invoke(false); return; }
        if (Array.IndexOf(Moods, mood) < 0)
        {
            Debug.LogError($"[Breez] Unknown mood '{mood}'. Use: {string.Join(", ", Moods)}");
            done?.Invoke(false);
            return;
        }

        string now = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        string body = Json.Obj(
            "p_mood", Json.Str(mood),
            "p_note", string.IsNullOrWhiteSpace(note) ? "null" : Json.Str(note.Trim()),
            "p_recorded_at", Json.Str(now));

        backend.Rpc("log_mood", body, (ok, msg) =>
        {
            Saved?.Invoke(ok, ok ? "บันทึกอารมณ์แล้ว" : msg);
            done?.Invoke(ok);
        }, queueOnFailure: true);
    }
}
