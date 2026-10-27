using UnityEngine;

// รับข้อมูลจาก ESP32 ผ่าน Ardity (SerialController จะเรียก OnMessageArrived / OnConnectionEvent)
// รูปแบบข้อความที่รองรับ:
//   "57"       ค่าแรงเป่า 0-100 (เหมือนเดิม)
//   "BAT:85"   ระดับแบตเตอรี่ % (ไม่บังคับ ใช้แสดงในหลังบ้าน)
public class BreathReceiver : MonoBehaviour
{
    public int breathLevel;

    [Tooltip("แสดงค่าที่ได้รับใน Console")]
    public bool logMessages = false;

    public void OnMessageArrived(string message)
    {
        message = message.Trim();
        DeviceService.NotifyMessage();

        if (message.StartsWith("BAT:"))
        {
            if (int.TryParse(message.Substring(4), out int battery))
                DeviceService.ReportBattery(battery);
            return;
        }

        if (int.TryParse(message, out int value))
        {
            breathLevel = Mathf.Clamp(value, 0, 100);

            if (logMessages)
                Debug.Log("Breath Level: " + breathLevel);
        }
    }

    // Ardity แจ้งเมื่อเสียบ/ถอดบอร์ด
    public void OnConnectionEvent(bool success)
    {
        DeviceService.SetSerialConnected(success);
        Debug.Log(success ? "Breath Gadget connected" : "Breath Gadget disconnected");
    }
}
