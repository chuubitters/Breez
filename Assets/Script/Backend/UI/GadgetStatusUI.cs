using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================
// ป้ายสถานะ Breath Gadget:  [ไอคอน]  ชื่อเครื่อง · เชื่อมต่อแล้ว   (78%)
// - ไอคอน/ชื่อ มาจากหน้า Gadget Setting (ตาราง devices)
// - สถานะเชื่อมต่ออัปเดตทุกวินาที
// - ถ้า scene นี้ยังไม่มี SerialController จะสร้างให้เอง (เช่นหน้า Home)
// - แบตเตอรี่ซ่อนไว้ก่อน (เปิดด้วย showBattery)
// - กดที่ป้ายแล้วไปหน้า Gadget Setting ได้ (openSceneOnClick)
// ลาก prefab Assets/Prefabs/Breez/GadgetStatus.prefab ใส่ Canvas ได้เลย
// =====================================================================
public class GadgetStatusUI : MonoBehaviour
{
    [Serializable]
    public class IconSprite { public string id; public Sprite sprite; }

    [Header("UI")]
    public Image iconImage;
    public TMP_Text label;
    public TMP_Text batteryText;
    public Image statusDot;          // ไม่ใส่ก็ได้
    public IconSprite[] icons;

    [Header("Text")]
    public string connectedText = "เชื่อมต่อแล้ว";
    public string disconnectedText = "ยังไม่เชื่อมต่อ";
    public string defaultName = "Breath Gadget";
    public string separator = " · ";

    [Header("Options")]
    public bool showBattery = false;
    [Tooltip("ถ้า scene นี้ไม่มี SerialController ให้สร้างเพื่อเช็คการเชื่อมต่อ")]
    public bool startSerialIfMissing = true;
    [Tooltip("กดที่ป้ายแล้วเปิด scene นี้ (เว้นว่าง = ไม่ทำอะไร)")]
    public string openSceneOnClick = "GadgetSetting";

    [Header("Colors")]
    public Color connectedColor = new Color32(122, 175, 104, 255);
    public Color disconnectedColor = new Color32(190, 190, 190, 255);
    public Color iconConnected = new Color32(85, 85, 85, 255);
    public Color iconDisconnected = new Color32(170, 170, 170, 255);

    void Start()
    {
        if (startSerialIfMissing) EnsureSerial();

        var btn = GetComponent<Button>();
        if (btn != null && !string.IsNullOrEmpty(openSceneOnClick))
            btn.onClick.AddListener(() => SceneManager.LoadScene(openSceneOnClick));

        DeviceService.Updated += OnDeviceUpdated;
        if (DeviceService.Current == null && BreezBackend.Instance.IsLoggedIn) DeviceService.Sync();
        InvokeRepeating(nameof(Refresh), 0f, 1f);
    }

    void OnDestroy() => DeviceService.Updated -= OnDeviceUpdated;

    void OnDeviceUpdated(DeviceInfo _) => Refresh();

    public void Refresh()
    {
        var d = DeviceService.Current;
        bool connected = DeviceService.StatusNow == "connected" || DeviceService.ServerSaysConnected;
        string name = d != null && !string.IsNullOrEmpty(d.device_name) ? d.device_name : defaultName;

        if (label != null) label.text = name + separator + (connected ? connectedText : disconnectedText);
        if (statusDot != null) statusDot.color = connected ? connectedColor : disconnectedColor;

        if (iconImage != null)
        {
            string id = d != null && !string.IsNullOrEmpty(d.device_icon) ? d.device_icon : "cat";
            var s = SpriteFor(id);
            if (s != null) iconImage.sprite = s;
            iconImage.color = connected ? iconConnected : iconDisconnected;
        }

        if (batteryText != null)
        {
            int? b = DeviceService.LastBattery;
            bool show = showBattery && connected && b.HasValue;
            batteryText.gameObject.SetActive(show);
            if (show) batteryText.text = b + "%";
            // เว้นที่ด้านขวาให้ตัวเลขแบตเตอรี่
            if (label != null)
            {
                var rt = label.rectTransform;
                rt.offsetMax = new Vector2(show ? -110f : -28f, rt.offsetMax.y);
            }
        }
    }

    Sprite SpriteFor(string id)
    {
        if (icons == null) return null;
        foreach (var i in icons) if (i.id == id) return i.sprite;
        return icons.Length > 0 ? icons[0].sprite : null;
    }

    // -----------------------------------------------------------------
    // สร้าง Ardity SerialController ชั่วคราวใน scene ที่ไม่มี
    // -----------------------------------------------------------------
    static void EnsureSerial()
    {
        foreach (var mb in FindObjectsOfType<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == "SerialController") return;

        Type type = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { type = asm.GetType("SerialController"); } catch { /* ข้าม */ }
            if (type != null) break;
        }
        if (type == null) return;

        var go = new GameObject("[Gadget Serial]");
        go.SetActive(false);                       // ตั้งค่าให้ครบก่อนเปิดพอร์ต
        go.AddComponent<BreathReceiver>();
        go.AddComponent<SerialPortAutoSelect>();
        var serial = go.AddComponent(type);
        var f = type.GetField("messageListener", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) f.SetValue(serial, go);
        go.SetActive(true);
    }
}
