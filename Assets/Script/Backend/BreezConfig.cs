// ค่าการเชื่อมต่อ Supabase ของ Breez
// publishable key เป็นค่าสาธารณะ ใส่ในเกมได้ (ข้อมูลถูกป้องกันด้วย Row Level Security)
// ห้ามใส่ service_role key ในเกมเด็ดขาด
public static class BreezConfig
{
    public const string SupabaseUrl = "https://ubdiwwmtbuzksyswmwod.supabase.co";
    public const string PublishableKey = "sb_publishable_E8-tt98y8Z0-ZxWUPvJgWg_2mYOAGdP";

    // ชื่อ scene
    public const string LoginScene = "Login";
    public const string HomeScene = "Home";

    // ส่งสถานะอุปกรณ์ทุกกี่วินาที (หลังบ้านถือว่าออนไลน์ถ้าอัปเดตภายใน 5 นาที)
    public const float DeviceHeartbeatSeconds = 60f;
}
