using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================
// หน้า Gadget Setting (แบบหน้า AirPods)
// - แตะไอคอน => เลือกไอคอนใหม่
// - แตะชื่อ => พิมพ์ชื่อใหม่ (บันทึกเมื่อกด Enter / คลิกออก)
// - สถานะเชื่อมต่อ + แบตเตอรี่ อัปเดตทุกวินาที
// - บันทึกลง Supabase (ตาราง devices) และส่ง NAME:/ICON: ไปที่บอร์ด
// สร้างพร้อมต่อสายด้วยเมนู Breez > Create Gadget Setting Scene
// =====================================================================
public class GadgetSettingsScreen : MonoBehaviour
{
    [Serializable]
    public class IconOption
    {
        public string id;          // cat / flower / cloud / star / heart / moon
        public Button button;
        public Image frame;        // กรอบที่เปลี่ยนสีเมื่อเลือก
        public Sprite sprite;
    }

    [Header("Settings")]
    public string backScene = "Home";
    public int maxNameLength = 20;

    [Header("UI")]
    public Button backButton;
    public Button iconButton;
    public Image iconImage;
    public GameObject iconPicker;
    public IconOption[] iconOptions;
    public TMP_InputField nameInput;
    public TMP_Text statusText;
    public Image statusDot;
    public TMP_Text batteryText;
    public TMP_Text toastText;

    [Header("Colors")]
    public Color connectedColor = new Color32(122, 175, 104, 255);
    public Color disconnectedColor = new Color32(170, 170, 170, 255);
    public Color optionNormal = new Color32(236, 238, 232, 255);
    public Color optionSelected = new Color32(166, 203, 69, 255);

    string currentIcon = "cat";
    string savedName = "";
    float toastUntil;

    void Start()
    {
        if (backButton != null) backButton.onClick.AddListener(() => SceneManager.LoadScene(backScene));
        if (iconButton != null) iconButton.onClick.AddListener(ToggleIconPicker);
        if (iconOptions != null)
            foreach (var opt in iconOptions)
            {
                var id = opt.id;
                if (opt.button != null) opt.button.onClick.AddListener(() => SelectIcon(id));
            }
        if (nameInput != null)
        {
            nameInput.characterLimit = maxNameLength;
            nameInput.onEndEdit.AddListener(SaveName);
        }
        if (iconPicker != null) iconPicker.SetActive(false);
        if (toastText != null) toastText.text = "";

        Load();
        InvokeRepeating(nameof(UpdateStatus), 0f, 1f);
    }

    void Update()
    {
        if (toastText != null && toastText.text.Length > 0 && Time.unscaledTime > toastUntil) toastText.text = "";
    }

    // -----------------------------------------------------------------

    void Load()
    {
        if (!BreezBackend.Instance.IsLoggedIn) { Toast("กรุณาเข้าสู่ระบบ"); return; }
        if (DeviceService.Current != null) Apply(DeviceService.Current);
        DeviceService.Sync(done: d => { if (this != null && d != null) Apply(d); });
    }

    void Apply(DeviceInfo d)
    {
        savedName = string.IsNullOrEmpty(d.device_name) ? "Breath Gadget" : d.device_name;
        if (nameInput != null && !nameInput.isFocused) nameInput.SetTextWithoutNotify(savedName);
        SetIcon(string.IsNullOrEmpty(d.device_icon) ? "cat" : d.device_icon);
        UpdateStatus();
    }

    public void ToggleIconPicker()
    {
        if (iconPicker != null) iconPicker.SetActive(!iconPicker.activeSelf);
    }

    public void SelectIcon(string id)
    {
        SetIcon(id);
        if (iconPicker != null) iconPicker.SetActive(false);
        Save(null, id);
    }

    public void SaveName(string value)
    {
        string name = (value ?? "").Trim();
        if (name.Length > maxNameLength) name = name.Substring(0, maxNameLength);
        if (string.IsNullOrEmpty(name))
        {
            if (nameInput != null) nameInput.SetTextWithoutNotify(savedName);
            return;
        }
        if (name == savedName) return;
        Save(name, null);
    }

    void Save(string name, string icon)
    {
        Toast("กำลังบันทึก...");
        DeviceService.Sync(name: name, icon: icon, done: d =>
        {
            if (this == null) return;
            if (d == null) { Toast("บันทึกไม่สำเร็จ"); return; }
            Apply(d);
            bool sent = DeviceService.CanSendToGadget;
            if (sent) DeviceService.PushIdentityToGadget();
            Toast(sent ? "บันทึกแล้ว และอัปเดตบนอุปกรณ์แล้ว" : "บันทึกแล้ว");
        });
    }

    void SetIcon(string id)
    {
        currentIcon = id;
        if (iconOptions == null) return;
        foreach (var opt in iconOptions)
        {
            bool sel = opt.id == id;
            if (opt.frame != null) opt.frame.color = sel ? optionSelected : optionNormal;
            if (sel && iconImage != null && opt.sprite != null) iconImage.sprite = opt.sprite;
        }
    }

    void UpdateStatus()
    {
        bool connected = DeviceService.StatusNow == "connected" || DeviceService.ServerSaysConnected;
        if (statusText != null)
            statusText.text = connected
                ? "เชื่อมต่ออยู่"
                : "ไม่ได้เชื่อมต่อ";
        if (statusDot != null) statusDot.color = connected ? connectedColor : disconnectedColor;

        if (batteryText != null)
        {
            int? b = DeviceService.LastBattery;
            if (!b.HasValue && DeviceService.Current != null && DeviceService.ServerSaysConnected && DeviceService.Current.battery_level > 0)
                b = DeviceService.Current.battery_level;
            batteryText.text = b.HasValue ? $"แบตเตอรี่ {b}%" : "";
        }
    }

    void Toast(string msg)
    {
        if (toastText == null) return;
        toastText.text = msg;
        toastUntil = Time.unscaledTime + 3f;
    }
}
