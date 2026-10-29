using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

// =====================================================================
// แปลข้อความในเกมเป็นภาษาไทย (ยกเว้นชื่อโหมด / ชื่อหัวข้อหลัก)
// ทำงานอัตโนมัติทุกครั้งที่ BreezThaiFontInstaller.Apply ทำงาน (ทุกเมนูสร้างหน้า)
// และสั่งทั้งเกมได้ด้วยเมนู Breez > Update Thai Text (ทั้งเกม)
// คงไว้เป็นภาษาอังกฤษ: Calm Flow, Power Release, Breathe & Focus, Soundscape, Rainy Forest,
//                     Windy Mountain, Breath Play, Weekly Summary, Setting, Breath Gadget ฯลฯ
// =====================================================================
public static class BreezThaiTranslator
{
    // ข้อความทั้งช่อง (ตรงตัว ไม่สนตัวพิมพ์เล็กใหญ่ / ช่องว่างหัวท้าย)
    static readonly Dictionary<string, string> Exact = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        // ทั่วไป
        { "Start", "เริ่ม" }, { "Back", "กลับ" }, { "Score", "คะแนน" }, { "points", "คะแนน" }, { "Result", "ผลลัพธ์" },
        { "End Session", "จบการฝึก" }, { "Pause", "พัก" }, { "Resume", "เล่นต่อ" }, { "Replay", "เล่นอีกครั้ง" },
        { "Home", "หน้าหลัก" }, { "Start Session", "เริ่มฝึก" }, { "Status", "สถานะ" }, { "Obstacle", "สิ่งกีดขวาง" },
        { "Time", "เวลา" }, { "Loading...", "กำลังโหลด..." }, { "Saving...", "กำลังบันทึก..." },
        { "GO!", "เริ่ม!" }, { "Inhale", "หายใจเข้า" }, { "Exhale", "หายใจออก" },
        { "MISSION COMPLETE!", "สำเร็จ!" }, { "TIME'S UP!", "หมดเวลา!" },
        // Login
        { "Welcome back", "ยินดีต้อนรับกลับ" }, { "Create account", "สมัครสมาชิก" },
        { "Sign in to save your breathing progress", "เข้าสู่ระบบเพื่อบันทึกผลการฝึกหายใจ" },
        { "Sign in", "เข้าสู่ระบบ" }, { "Your name", "ชื่อของคุณ" }, { "e.g. Pim", "เช่น พิม" },
        { "Email", "อีเมล" }, { "Password", "รหัสผ่าน" }, { "At least 6 characters", "อย่างน้อย 6 ตัวอักษร" },
        { "New here? Create account", "ยังไม่มีบัญชี? สมัครสมาชิก" }, { "Have an account? Sign in", "มีบัญชีแล้ว? เข้าสู่ระบบ" },
        // Gadget
        { "Not connected", "ไม่ได้เชื่อมต่อ" }, { "Connected", "เชื่อมต่อแล้ว" }, { "Device name", "ชื่ออุปกรณ์" }, { "icon", "ไอคอน" },
        { "Breath Gadget · Connected", "Breath Gadget · เชื่อมต่อแล้ว" },
        // Weekly Summary
        { "How are you feeling today?", "วันนี้คุณรู้สึกอย่างไรบ้าง?" }, { "Sessions", "จำนวนครั้งที่เล่น" },
        { "Average Mood", "อารมณ์ที่พบบ่อย" }, { "Sessions by game", "จำนวนครั้งแต่ละโหมด" },
        { "Recent Play", "การเล่นล่าสุด" }, { "No games this week yet", "ยังไม่มีการเล่นในสัปดาห์นี้" },
        { "streak", "เล่นต่อเนื่อง" }, { "lovely", "อบอุ่นใจ" }, { "happy", "มีความสุข" }, { "bored", "เบื่อ" },
        { "angry", "โกรธ" }, { "stress", "เครียด" },
        // Calm Flow
        { "1 minute", "1 นาที" }, { "2 minutes", "2 นาที" }, { "3 minutes", "3 นาที" },
        { "เลือก Session Duration", "เลือกระยะเวลาฝึก" },
        { "Flowers Grown", "ดอกไม้ที่ปลูก" }, { "Largest Flower", "ดอกใหญ่ที่สุด" },
        { "Longest Exhale", "หายใจออกนานสุด" }, { "Total Exhale Time", "เวลาหายใจออกรวม" },
    };

    // แทนบางส่วนของข้อความ
    static readonly KeyValuePair<string, string>[] Partial =
    {
        new KeyValuePair<string, string>("<size=22>Sessions</size>", "<size=22>ครั้ง</size>"),
        new KeyValuePair<string, string>("status : ", "สถานะ : "),
        new KeyValuePair<string, string>("<b>Inhale</b>", "<b>หายใจเข้า</b>"),
        new KeyValuePair<string, string>("<b>Exhale</b>", "<b>หายใจออก</b>"),
        new KeyValuePair<string, string>("Total Score: ", "คะแนนรวม "),
        new KeyValuePair<string, string>("Time Taken: ", "เวลาที่ใช้ "),
        new KeyValuePair<string, string>("Score: ", "คะแนน "),
        new KeyValuePair<string, string>("Time: ", "เวลา "),
        new KeyValuePair<string, string>(" Seconds", " วินาที"),
        new KeyValuePair<string, string>(" Second", " วินาที"),
    };

    [MenuItem("Breez/Update Thai Text (ทั้งเกม)")]
    public static void TranslateProject()
    {
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BreezThaiFontInstaller.EnsureFonts();
        int n = BreezThaiFontInstaller.ApplyToProject();
        EditorUtility.DisplayDialog("Breez", "อัปเดตข้อความภาษาไทยทั้งเกมแล้ว (" + n + " ข้อความ)\n\nชื่อโหมดและชื่อหัวข้อหลักยังเป็นภาษาอังกฤษตามเดิม", "OK");
    }

    public static string Translate(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string trimmed = text.Trim();
        if (Exact.TryGetValue(trimmed, out var th)) return th;
        string result = text;
        foreach (var kv in Partial)
            if (result.Contains(kv.Key)) result = result.Replace(kv.Key, kv.Value);
        return result;
    }

    /// <summary>แปลข้อความ TMP + ค่าข้อความใน component ของ Breez คืนค่า true ถ้ามีการเปลี่ยน</summary>
    public static bool Apply(TMP_Text t)
    {
        if (t == null) return false;
        var th = Translate(t.text);
        if (th == t.text) return false;
        Undo.RecordObject(t, "Thai text");
        t.text = th;
        EditorUtility.SetDirty(t);
        return true;
    }

    /// <summary>ค่าข้อความที่ตั้งไว้ใน component (เช่น prefix ของป้ายหายใจ)</summary>
    public static int ApplyComponents(GameObject root)
    {
        int c = 0;
        foreach (var b in root.GetComponentsInChildren<BreathStatusUI>(true))
            if (b.prefix == "status : ") { Undo.RecordObject(b, "Thai text"); b.prefix = "สถานะ : "; Dirty(b); c++; }
        foreach (var a in root.GetComponentsInChildren<AccountActions>(true))
            if (a.greetingFormat == "Hi, {0}") { Undo.RecordObject(a, "Thai text"); a.greetingFormat = "สวัสดี {0}"; Dirty(a); c++; }
        foreach (var g in root.GetComponentsInChildren<CalmFlowGame>(true))
            if (g.subtitleFormat == "{0}-Minute Session Complete") { Undo.RecordObject(g, "Thai text"); g.subtitleFormat = "ฝึกครบ {0} นาทีแล้ว"; Dirty(g); c++; }
        foreach (var p in root.GetComponentsInChildren<PWGameManager>(true))
        {
            bool ch = false;
            Undo.RecordObject(p, "Thai text");
            if (p.winText == "MISSION COMPLETE!") { p.winText = "สำเร็จ!"; ch = true; }
            if (p.loseText == "TIME'S UP!") { p.loseText = "หมดเวลา!"; ch = true; }
            if (ch) { Dirty(p); c++; }
        }
        return c;
    }

    static void Dirty(Object o)
    {
        EditorUtility.SetDirty(o);
        if (PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }
}
