using UnityEngine;

// รับข้อมูลจาก ESP32 ผ่าน Ardity (SerialController จะเรียก OnMessageArrived / OnConnectionEvent)
// รูปแบบข้อความที่รองรับ:
//   "57"       ค่าแรงเป่า 0-100 (เหมือนเดิม)
//   "BAT:85"   ระดับแบตเตอรี่ % (ไม่บังคับ ใช้แสดงในหลังบ้าน)
//   "QS:LOG,7,4,76,85,1793500000"  ผล Quick Session ที่เก็บในเครื่อง (id, รอบ, วินาที, % เป่า, เวลาจบ unix)
//                     => บันทึกลงประวัติ/หลังบ้าน แล้วตอบ "QS:ACK,7" ให้บอร์ดลบออก
//   "QS:DONE,4,76,85"  (เฟิร์มแวร์รุ่นเก่า) ฝึกครบ => บันทึก
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

    static readonly System.Collections.Generic.HashSet<long> savingIds = new System.Collections.Generic.HashSet<long>();

    void HandleQuickSession(string payload)
    {
        if (payload.StartsWith("LOG,")) { HandleQuickLog(payload); return; }
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

    /// <summary>ผลที่ค้างในเครื่อง: บันทึก (ใช้เวลาจริงที่ฝึก) แล้วตอบ ACK</summary>
    void HandleQuickLog(string payload)
    {
        var p = payload.Split(',');
        if (p.Length < 6) return;
        if (!long.TryParse(p[1], out long id) || !int.TryParse(p[2], out int rounds) || !int.TryParse(p[3], out int seconds) ||
            !int.TryParse(p[4], out int blowPct) || !long.TryParse(p[5], out long endedUnix)) return;
        if (!BreezBackend.Instance.IsLoggedIn) return;          // ยังไม่ล็อกอิน: เก็บไว้ในเครื่องก่อน ส่งใหม่ทีหลัง
        if (savingIds.Contains(id)) return;                     // กำลังบันทึกอยู่ / มีหลาย receiver
        savingIds.Add(id);

        System.DateTime? endedUtc = null;
        if (endedUnix > 1600000000) endedUtc = System.DateTimeOffset.FromUnixTimeSeconds(endedUnix).UtcDateTime;
        Debug.Log($"[Breez] Quick Session from gadget #{id}: {rounds} rounds, {seconds}s, blow {blowPct}%" + (endedUtc.HasValue ? $", ended {endedUtc:u}" : ""));

        GameDataRecorder.RecordQuickSession(seconds, rounds, blowPct, ok =>
        {
            savingIds.Remove(id);
            if (!ok) return;                                    // ไม่สำเร็จ: บอร์ดจะส่งซ้ำเอง
            DeviceService.SendToGadget("QS:ACK," + id);
            QuickSessionCompleted?.Invoke(rounds, seconds, blowPct);
        }, endedUtc);
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
        // ส่งเวลาให้บอร์ด => บอร์ดส่ง Quick Session ที่ฝึกตอนไม่ได้เสียบเกมมาให้บันทึก
        DeviceService.SendToGadget("TIME:" + System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }
}
