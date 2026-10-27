using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// =====================================================================
// เลือกพอร์ต USB ของ ESP32 ให้ Ardity SerialController อัตโนมัติ
// ใส่ไว้ที่ GameObject เดียวกับ SerialController (เมนู Breez > Configure Serial for ESP32 ใส่ให้)
// ทำงานก่อน SerialController เปิดพอร์ต (DefaultExecutionOrder -1000)
//
// ลำดับการเลือกพอร์ต:
//   1. พอร์ตที่บันทึกไว้ (PlayerPrefs "breez.serialPort") ถ้ายังมีอยู่
//   2. Mac:     /dev/cu.usbmodem*  (ESP32-C3 USB)  แล้วค่อย /dev/cu.usbserial*, wchusbserial*, SLAB*
//      Windows: COM ตัวเลขมากสุด (ไม่ใช่ COM1)
//      Linux:   /dev/ttyACM*, /dev/ttyUSB*
//   3. ถ้าไม่เจอเลย ใช้ค่าที่ตั้งใน Inspector
// =====================================================================
[DefaultExecutionOrder(-1000)]
public class SerialPortAutoSelect : MonoBehaviour
{
    public const string PrefKey = "breez.serialPort";

    [Tooltip("ปิดถ้าต้องการใช้ Port Name ใน SerialController ตรงๆ")]
    public bool autoDetect = true;
    public int baudRate = 115200;
    [Tooltip("ESP32-C3 แบบ USB ในตัว ควรเปิด DTR")]
    public bool dtrEnable = true;

    public static string LastSelectedPort { get; private set; }

    void Awake()
    {
        var serial = FindSerialController();
        if (serial == null)
        {
            Debug.LogWarning("[Breez] SerialPortAutoSelect: no SerialController on this GameObject.");
            return;
        }

        SetField(serial, "baudRate", baudRate);
        SetField(serial, "dtrEnable", dtrEnable);

        string current = GetField<string>(serial, "portName");
        string port = autoDetect ? PickPort(current) : current;
        if (!string.IsNullOrEmpty(port)) SetField(serial, "portName", port);
        LastSelectedPort = port;
        Debug.Log($"[Breez] Serial port: {port} @ {baudRate} (found: {string.Join(", ", ListCandidatePorts())})");
    }

    /// <summary>จำพอร์ตนี้ไว้ใช้ครั้งต่อไป (เช่น เมื่อมีหลายพอร์ตให้เลือก)</summary>
    public static void RememberPort(string port)
    {
        PlayerPrefs.SetString(PrefKey, port ?? "");
        PlayerPrefs.Save();
    }

    public static string PickPort(string fallback)
    {
        var ports = ListCandidatePorts();
        string saved = PlayerPrefs.GetString(PrefKey, "");
        if (!string.IsNullOrEmpty(saved) && ports.Contains(saved)) return saved;
        if (ports.Count > 0) return ports[0];
        return fallback;
    }

    /// <summary>รายชื่อพอร์ตที่น่าจะเป็นบอร์ด เรียงตามความน่าจะเป็น</summary>
    public static List<string> ListCandidatePorts()
    {
        var result = new List<string>();
        try
        {
            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
            {
                foreach (var pattern in new[] { "cu.usbmodem*", "cu.usbserial*", "cu.wchusbserial*", "cu.SLAB*" })
                    result.AddRange(Directory.GetFiles("/dev", pattern).OrderBy(p => p));
            }
            else if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                result.AddRange(WindowsPorts()
                    .Where(p => p != "COM1")
                    .OrderByDescending(p => int.TryParse(p.Substring(3), out int n) ? n : 0));
            }
            else
            {
                foreach (var pattern in new[] { "ttyACM*", "ttyUSB*" })
                    result.AddRange(Directory.GetFiles("/dev", pattern).OrderBy(p => p));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Breez] Could not list serial ports: " + e.Message);
        }
        return result.Distinct().ToList();
    }

    // System.IO.Ports ผ่าน reflection เพื่อไม่ให้ compile พังถ้า API level ไม่รองรับ
    static IEnumerable<string> WindowsPorts()
    {
        var type = Type.GetType("System.IO.Ports.SerialPort, System") ??
                   Type.GetType("System.IO.Ports.SerialPort, System.IO.Ports");
        var method = type?.GetMethod("GetPortNames", BindingFlags.Public | BindingFlags.Static);
        return method?.Invoke(null, null) as string[] ?? new string[0];
    }

    // ---------- เข้าถึงฟิลด์ของ Ardity SerialController โดยไม่อ้างชนิดตรงๆ ----------
    MonoBehaviour FindSerialController()
    {
        foreach (var mb in GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == "SerialController") return mb;
        return null;
    }

    static void SetField(object target, string name, object value)
    {
        var f = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && f.FieldType.IsInstanceOfType(value)) f.SetValue(target, value);
    }

    static T GetField<T>(object target, string name)
    {
        var f = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return f != null && f.GetValue(target) is T v ? v : default;
    }
}
