using System.Globalization;
using System.Text;

// ตัวช่วยสร้าง JSON แบบง่าย (JsonUtility สร้าง object ซ้อนแบบ dynamic ไม่ได้)
public static class Json
{
    public static string Str(string value)
    {
        if (value == null) return "null";
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    public static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
    public static string Num(int? value) => value.HasValue ? Num(value.Value) : "null";
    public static string Num(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>สร้าง object จากคู่ key / ค่าที่แปลงเป็น JSON แล้ว เช่น Obj("a", Json.Num(1), "b", Json.Str("x"))</summary>
    public static string Obj(params string[] keyValues)
    {
        var sb = new StringBuilder("{");
        for (int i = 0; i + 1 < keyValues.Length; i += 2)
        {
            if (i > 0) sb.Append(',');
            sb.Append(Str(keyValues[i])).Append(':').Append(keyValues[i + 1] ?? "null");
        }
        return sb.Append('}').ToString();
    }
}
