using UnityEngine;

// รับข้อมูลจาก ESP32 ผ่าน Ardity (SerialController จะเรียก OnMessageArrived / OnConnectionEvent)
// รูปแบบข้อความที่รองรับ:
//   "57"       ค่าแรงเป่า 0-100 (เหมือนเดิม)
//   "BAT:85"   ระดับแบตเตอรี่ % (ไม่บังคับ ใช้แสดงในหลังบ้าน)
//   "QS:DONE,4,76,85"  ฝึก Quick Session 4-7-8 บนตัวเครื่องครบ (รอบ, วินาที, % เป่า) => บันทึกลงประวัติ
// เมื่อบอร์ดเชื่อมต่อ จะส่ง "NAME:..." และ "ICON:..." กลับไปให้บอร์ดแสดงบนจอ
public class BreathReceiver : MonoBehaviour
{
    public int breathLevel;

    [Tooltip("แสดงค่าที่ได้รับใน Console")]
    public bool logMessages = false;

    void Awake()
    {
        // หา Ardity SerialController (อยู่ที่ GameObject เดียวกันหรือที่ใดก็ได้ใน scene)
        foreach (var mb in GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == "SerialController") { DeviceService.RegisterSerial(mb.gameObject); return; }
        foreach (var mb in FindObjectsOfType<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == "SerialController") { DeviceService.RegisterSerial(mb.gameObject); return; }
    }

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

        if (message.StartsWith("QS:"))
        {
            HandleQuickSession(message.Substring(3));
            return;
        }

        if (int.TryParse(message, out int value))
        {
            breathLevel = Mathf.Clamp(value, 0, 100);

            if (logMessages)
                Debug.Log("Breath Level: " + breathLevel);
        }
    }

    /// <summary>Quick Session ที่ฝึกบนตัวเครื่อง (จอกลม) ระหว่างเสียบสายกับเกม => บันทึกลงประวัติการเล่น</summary>
    public static event System.Action<int, int, int> QuickSessionCompleted;   // rounds, seconds, blowPct
    static float lastQuickSave = -999f;

    void HandleQuickSession(string payload)
    {
        if (!payload.StartsWith("DONE")) return;              // START / STOP: ไม่ต้องทำอะไร
        var parts = payload.Split(',');
        if (parts.Length < 4) return;
        if (!int.TryParse(parts[1], out int rounds) || !int.TryParse(parts[2], out int seconds) || !int.TryParse(parts[3], out int blowPct)) return;
        // มี BreathReceiver หลายตัวในบาง scene => บันทึกครั้งเดียว
        if (Time.unscaledTime - lastQuickSave < 5f) return;
        lastQuickSave = Time.unscaledTime;
        Debug.Log($"[Breez] Quick Session done on gadget: {rounds} rounds, {seconds}s, blow {blowPct}%");
        GameDataRecorder.RecordQuickSession(seconds, rounds, blowPct);
        QuickSessionCompleted?.Invoke(rounds, seconds, blowPct);
    }

    // Ardity แจ้งเมื่อเสียบ/ถอดบอร์ด
    public void OnConnectionEvent(bool success)
    {
        DeviceService.SetSerialConnected(success);
        Debug.Log(success ? "Breath Gadget connected" : "Breath Gadget disconnected");

        if (!success) return;
        // ส่งชื่อ + ไอคอนให้บอร์ดแสดง (เหมือน AirPods ที่โชว์ชื่อเครื่อง)
        if (DeviceService.Current != null) DeviceService.PushIdentityToGadget();
        else DeviceService.Sync(done: _ => DeviceService.PushIdentityToGadget());
    }
}
