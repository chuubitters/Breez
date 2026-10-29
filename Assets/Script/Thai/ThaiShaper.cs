using System.Collections.Generic;
using System.Text;

// =====================================================================
// ThaiShaper: ทำให้ภาษาไทยแสดงถูกใน TextMeshPro (TMP ไม่จัดตำแหน่งวรรณยุกต์/สระให้)
// - แทนคลัสเตอร์ "พยัญชนะ + สระบน/ล่าง + วรรณยุกต์" ด้วย glyph ที่จัดตำแหน่งไว้แล้ว
//   (อยู่ในช่อง PUA ของฟอนต์ Breez Seed TH, ตารางสร้างจาก tools/thai-font/build_thai_font.py)
// - ใช้ผ่าน component ThaiText (ใส่ให้อัตโนมัติด้วยเมนู Breez > Install LINE Seed Font)
//   หรือเรียกเองได้: ThaiShaper.Fix("ผลลัพธ์")
// - เรียกซ้ำกี่ครั้งก็ได้ผลเหมือนเดิม
// =====================================================================
public static class ThaiShaper
{
    static Dictionary<string, string> map;
    static int maxKey;

    static void Init()
    {
        map = new Dictionary<string, string>(ThaiShapingTable.Count);
        for (int i = 0; i < ThaiShapingTable.Keys.Length; i++)
        {
            map[ThaiShapingTable.Keys[i]] = ThaiShapingTable.Values[i];
            if (ThaiShapingTable.Keys[i].Length > maxKey) maxKey = ThaiShapingTable.Keys[i].Length;
        }
    }

    static bool IsConsonant(char c) => c >= 'ก' && c <= 'ฮ';

    public static bool HasThai(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char c in s) if (c >= '฀' && c <= '๿') return true;
        return false;
    }

    public static string Fix(string s)
    {
        if (!HasThai(s)) return s;
        if (map == null) Init();

        StringBuilder sb = null;
        int i = 0, copied = 0;
        while (i < s.Length)
        {
            if (IsConsonant(s[i]) && i + 1 < s.Length)
            {
                bool hit = false;
                for (int len = System.Math.Min(maxKey, s.Length - i); len >= 2; len--)
                {
                    if (!map.TryGetValue(s.Substring(i, len), out var rep)) continue;
                    if (sb == null) sb = new StringBuilder(s.Length);
                    sb.Append(s, copied, i - copied);
                    sb.Append(rep);
                    i += len;
                    copied = i;
                    hit = true;
                    break;
                }
                if (hit) continue;
            }
            i++;
        }
        if (sb == null) return s;
        sb.Append(s, copied, s.Length - copied);
        return sb.ToString();
    }
}
