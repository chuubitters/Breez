using System;
using System.Globalization;

// =====================================================================
// ข้อความภาษาไทยที่ใช้ร่วมกันในเกม (ชื่ออารมณ์, รายละเอียดผลการเล่น, วันที่)
// ชื่อโหมด (Calm Flow, Power Release ...) คงเป็นภาษาอังกฤษตามเดิม
// =====================================================================
public static class BreezText
{
    public static string Mood(string mood)
    {
        switch ((mood ?? "").ToLowerInvariant())
        {
            case "lovely": return "อบอุ่นใจ";
            case "happy": return "มีความสุข";
            case "bored": return "เบื่อ";
            case "angry": return "โกรธ";
            case "stress": return "เครียด";
            default: return mood ?? "";
        }
    }

    static readonly string[] WeekdayShort = { "อา.", "จ.", "อ.", "พ.", "พฤ.", "ศ.", "ส." };
    static readonly string[] WeekdayLong = { "อาทิตย์", "จันทร์", "อังคาร", "พุธ", "พฤหัสบดี", "ศุกร์", "เสาร์" };
    static readonly string[] MonthShort = { "ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค." };

    public static string WeekdayShortOf(DateTime d) => WeekdayShort[(int)d.DayOfWeek];

    /// <summary>เช่น "วันอังคารที่ 27 ต.ค. 2026"</summary>
    public static string LongDate(DateTime d) => $"วัน{WeekdayLong[(int)d.DayOfWeek]}ที่ {d.Day} {MonthShort[d.Month - 1]} {d.Year}";

    /// <summary>เช่น "21 – 27 ต.ค. 2026" หรือ "28 ต.ค. – 3 พ.ย. 2026"</summary>
    public static string Range(DateTime a, DateTime b) =>
        a.Month == b.Month
            ? $"{a.Day} – {b.Day} {MonthShort[b.Month - 1]} {b.Year}"
            : $"{a.Day} {MonthShort[a.Month - 1]} – {b.Day} {MonthShort[b.Month - 1]} {b.Year}";

    public static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Clock(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

    /// <summary>รายละเอียดผลของแต่ละโหมด 1 บรรทัด ("" = ไม่มี)</summary>
    public static string SessionResult(SummarySession s) =>
        SessionResult(s.mode, s.obstacles_avoided, s.balloons_popped, s.balloons_total, s.flowers_grown, s.largest_flower_pct,
                      s.flame_out_count, s.scene, s.breath_cycles);

    public static string SessionResult(string mode, int obstaclesAvoided, int balloonsPopped, int balloonsTotal,
                                       int flowersGrown, int largestFlowerPct, int flameOutCount,
                                       string scene = null, int breathCycles = -1)
    {
        if (obstaclesAvoided >= 0) return $"ข้ามสิ่งกีดขวางได้ทั้งหมด {obstaclesAvoided} ชิ้น";
        if (balloonsTotal >= 0) return $"ลูกโป่งที่เป่าได้ {balloonsPopped}/{balloonsTotal}";
        if (mode == GameDataRecorder.CalmFlow)
            return flowersGrown > 0 ? $"ปลูกดอกไม้ {flowersGrown} ดอก · ใหญ่สุด {largestFlowerPct}%" : "ยังไม่มีดอกไม้บาน";
        if (mode == GameDataRecorder.Soundscape)
        {
            string where = string.IsNullOrEmpty(scene) ? "" : scene + " · ";
            return breathCycles > 0 ? $"{where}หายใจครบ {breathCycles} รอบ" : where + "ฝึกหายใจ เข้า 5 วิ ออก 5 วิ";
        }
        if (mode == GameDataRecorder.QuickSession) return "ฝึกหายใจแบบ 4-7-8";
        return "";
    }
}
