using System;
using UnityEngine;

[Serializable]
public class DeviceInfo
{
    public string device_id;
    public string device_name;
    public string device_icon;
    public int battery_level;
    public string connection_status;
    public string updated_at;
}

// =====================================================================
// สถานะ Breath Gadget (ESP32) ของผู้เล่น
// BreathReceiver จะเรียก NotifyMessage / ReportBattery / SetSerialConnected
// BreezBackend จะส่งสถานะขึ้น Supabase อัตโนมัติ (หลังบ้านเห็นออนไลน์/แบตเตอรี่)
// =====================================================================
public static class DeviceService
{
    public static DeviceInfo Current { get; private set; }
    public static int? LastBattery { get; private set; }
    public static bool SerialConnected { get; private set; }
    static float lastMessageTime = -999f;

    public static event Action<DeviceInfo> Updated;

    /// <summary>ได้รับข้อมูลจาก ESP32 ภายใน 10 วินาทีล่าสุด</summary>
    public static bool IsReceiving => Time.realtimeSinceStartup - lastMessageTime < 10f;

    public static string StatusNow => (SerialConnected || IsReceiving) ? "connected" : "disconnected";

    // ---------- ส่งข้อมูลไปที่ ESP32 (ผ่าน Ardity SerialController) ----------
    static GameObject serialObject;

    /// <summary>BreathReceiver เรียกเพื่อบอกว่า SerialController อยู่ที่ GameObject ไหน</summary>
    public static void RegisterSerial(GameObject go) => serialObject = go;

    public static bool CanSendToGadget => serialObject != null && SerialConnected;

    /// <summary>ส่ง 1 บรรทัดไปที่บอร์ด เช่น "NAME:Pim's Gadget"</summary>
    public static bool SendToGadget(string line)
    {
        if (serialObject == null) return false;
        serialObject.SendMessage("SendSerialMessage", line, SendMessageOptions.DontRequireReceiver);
        return true;
    }

    /// <summary>ส่งชื่อและไอคอนไปแสดงบนจอของ Breath Gadget</summary>
    public static void PushIdentityToGadget()
    {
        if (Current == null) return;
        SendToGadget("NAME:" + (Current.device_name ?? "").Replace("\n", " "));
        SendToGadget("ICON:" + Current.device_icon);
    }

    /// <summary>ออนไลน์ตามข้อมูลบนเซิร์ฟเวอร์ (อัปเดตภายใน 2 นาที)</summary>
    public static bool ServerSaysConnected
    {
        get
        {
            if (Current == null || Current.connection_status != "connected") return false;
            if (!DateTime.TryParse(Current.updated_at, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t)) return false;
            return (DateTime.UtcNow - t.ToUniversalTime()).TotalMinutes < 2;
        }
    }

    public static void NotifyMessage() => lastMessageTime = Time.realtimeSinceStartup;
    public static void ReportBattery(int percent) => LastBattery = Mathf.Clamp(percent, 0, 100);
    public static void SetSerialConnected(bool connected) => SerialConnected = connected;

    /// <summary>
    /// โหลด/สร้าง/อัปเดตอุปกรณ์ของผู้เล่น (ส่ง null = ไม่เปลี่ยนค่านั้น)
    /// </summary>
    public static void Sync(int? battery = null, string status = null, string name = null, string icon = null,
                            Action<DeviceInfo> done = null)
    {
        if (!BreezBackend.Instance.IsLoggedIn) { done?.Invoke(null); return; }

        string body = Json.Obj(
            "p_battery", Json.Num(battery),
            "p_status", status == null ? "null" : Json.Str(status),
            "p_name", name == null ? "null" : Json.Str(name),
            "p_icon", icon == null ? "null" : Json.Str(icon));

        BreezBackend.Instance.Rpc("sync_my_device", body, (ok, text) =>
        {
            if (ok)
            {
                try { Current = JsonUtility.FromJson<DeviceInfo>(text); }
                catch (Exception e) { Debug.LogWarning("[Breez] Device parse error: " + e.Message); }
                Updated?.Invoke(Current);
            }
            done?.Invoke(ok ? Current : null);
        });
    }
}
