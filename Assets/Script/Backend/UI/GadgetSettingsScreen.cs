using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================
// หน้า Gadget Setting
// - แก้ชื่อ / เลือกไอคอน / เลือกสีไอคอน แล้วกด "บันทึก" (ยังไม่บันทึกจนกว่าจะกด)
// - ตัวอย่างไอคอนแสดงผลทันทีที่เลือก
// - สถานะเชื่อมต่อ + แบตเตอรี่ อัปเดตทุกวินาที
// - บันทึกลง Supabase (ตาราง devices) และส่ง NAME:/ICON:/COLOR: ไปที่บอร์ด
// หน้าตาสร้างด้วยเมนู Breez > Restyle Gadget Setting
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
        public Image icon;         // ไอคอนในปุ่ม (ระบายสีตามสีที่เลือก)
    }

    [Serializable]
    public class ColorOption
    {
        public string hex;         // #A9C77E
        public Button button;
        public Image swatch;
        public Image ring;         // วงรอบเมื่อเลือก
    }

    [Header("Settings")]
    public string backScene = "Home";
    public int maxNameLength = 20;

    [Header("UI")]
    public Button backButton;
    public Button iconButton;
    public Image iconImage;
    public Image iconBackground;       // พื้นหลังวงกลมของไอคอนตัวอย่าง (สีอ่อนของสีที่เลือก)
    public GameObject iconPicker;      // แบบเก่า (กดไอคอนแล้วเปิด) — แบบใหม่แสดงตลอด
    public IconOption[] iconOptions;
    public ColorOption[] colorOptions;
    public TMP_InputField nameInput;
    public TMP_Text previewName;
    public TMP_Text statusText;
    public Image statusDot;
    public TMP_Text batteryText;
    public TMP_Text toastText;

    [Header("บันทึก")]
    public Button saveButton;
    public TMP_Text saveLabel;
    public Button resetButton;

    [Header("Colors")]
    public Color connectedColor = new Color32(122, 175, 104, 255);
    public Color disconnectedColor = new Color32(170, 170, 170, 255);
    public Color optionNormal = new Color32(240, 242, 234, 255);
    public Color optionSelected = new Color32(212, 229, 157, 255);

    // ค่าที่บันทึกแล้ว / ค่าที่กำลังแก้
    string savedName = "Breath Gadget", savedIcon = DeviceInfo.DefaultIcon, savedHex = DeviceInfo.DefaultColor;
    string editName, editIcon, editHex;
    bool saving, leaveArmed;
    float toastUntil, leaveArmedUntil;

    bool Dirty => editName != savedName || editIcon != savedIcon || !string.Equals(editHex, savedHex, StringComparison.OrdinalIgnoreCase);

    void Start()
    {
        editName = savedName; editIcon = savedIcon; editHex = savedHex;

        if (backButton != null) { backButton.onClick.RemoveAllListeners(); backButton.onClick.AddListener(Back); }
        if (iconButton != null) iconButton.onClick.AddListener(ToggleIconPicker);
        if (iconOptions != null)
            foreach (var opt in iconOptions)
            {
                var id = opt.id;
                if (opt.button != null) opt.button.onClick.AddListener(() => SelectIcon(id));
            }
        if (colorOptions != null)
            foreach (var opt in colorOptions)
            {
                var hex = opt.hex;
                if (opt.swatch != null) opt.swatch.color = DeviceInfo.ParseColor(hex);
                if (opt.button != null) opt.button.onClick.AddListener(() => SelectColor(hex));
            }
        if (nameInput != null)
        {
            nameInput.characterLimit = maxNameLength;
            nameInput.onValueChanged.AddListener(v => { editName = Clean(v); RefreshEdits(); });
            nameInput.onSubmit.AddListener(_ => Save());
        }
        if (saveButton != null) saveButton.onClick.AddListener(Save);
        if (resetButton != null) resetButton.onClick.AddListener(ResetEdits);
        if (toastText != null) toastText.text = "";

        RefreshEdits();
        Load();
        InvokeRepeating(nameof(UpdateStatus), 0f, 1f);
    }

    void Update()
    {
        if (toastText != null && toastText.text.Length > 0 && Time.unscaledTime > toastUntil) toastText.text = "";
        if (leaveArmed && Time.unscaledTime > leaveArmedUntil) leaveArmed = false;
    }

    // -----------------------------------------------------------------

    void Load()
    {
        if (!BreezBackend.Instance.IsLoggedIn) { Toast("กรุณาเข้าสู่ระบบ"); return; }
        if (DeviceService.Current != null) ApplySaved(DeviceService.Current, keepEdits: false);
        DeviceService.Sync(done: d => { if (this != null && d != null) ApplySaved(d, keepEdits: Dirty); });
    }

    void ApplySaved(DeviceInfo d, bool keepEdits)
    {
        savedName = string.IsNullOrEmpty(d.device_name) ? "Breath Gadget" : d.device_name;
        savedIcon = d.IconId;
        savedHex = d.IconHex;
        if (!keepEdits)
        {
            editName = savedName; editIcon = savedIcon; editHex = savedHex;
            if (nameInput != null && !nameInput.isFocused) nameInput.SetTextWithoutNotify(savedName);
        }
        RefreshEdits();
        UpdateStatus();
    }

    public void ToggleIconPicker()
    {
        if (iconPicker != null) iconPicker.SetActive(!iconPicker.activeSelf);
    }

    public void SelectIcon(string id)
    {
        editIcon = id;
        if (iconPicker != null && iconPicker != gameObject) iconPicker.SetActive(false);
        RefreshEdits();
    }

    public void SelectColor(string hex)
    {
        editHex = hex;
        RefreshEdits();
    }

    public void ResetEdits()
    {
        editName = savedName; editIcon = savedIcon; editHex = savedHex;
        if (nameInput != null) nameInput.SetTextWithoutNotify(savedName);
        RefreshEdits();
    }

    /// <summary>ปุ่มกลับ: ถ้ายังไม่บันทึก เตือนก่อน (กดซ้ำเพื่อออก)</summary>
    public void Back()
    {
        if (Dirty && !leaveArmed)
        {
            leaveArmed = true;
            leaveArmedUntil = Time.unscaledTime + 3f;
            Toast("ยังไม่ได้บันทึก กด \"บันทึก\" หรือกดกลับอีกครั้งเพื่อออก");
            return;
        }
        string target = !string.IsNullOrEmpty(GadgetStatusUI.ReturnScene) && GadgetStatusUI.ReturnScene != SceneManager.GetActiveScene().name
            ? GadgetStatusUI.ReturnScene : backScene;
        GadgetStatusUI.ReturnScene = null;
        SceneManager.LoadScene(target);
    }

    public void Save()
    {
        if (saving) return;
        if (string.IsNullOrEmpty(editName)) { Toast("กรุณาใส่ชื่ออุปกรณ์"); return; }
        if (!Dirty) { Toast("ไม่มีอะไรเปลี่ยน"); return; }
        if (!BreezBackend.Instance.IsLoggedIn) { Toast("กรุณาเข้าสู่ระบบ"); return; }

        saving = true;
        RefreshEdits();
        Toast("กำลังบันทึก...");
        string name = editName != savedName ? editName : null;
        string icon = DeviceInfo.ComposeIcon(editIcon, editHex);
        DeviceService.Sync(name: name, icon: icon, done: d =>
        {
            if (this == null) return;
            saving = false;
            if (d == null) { RefreshEdits(); Toast("บันทึกไม่สำเร็จ ลองใหม่อีกครั้ง"); return; }
            ApplySaved(d, keepEdits: false);
            bool sent = DeviceService.CanSendToGadget;
            if (sent) DeviceService.PushIdentityToGadget();
            Toast(sent ? "บันทึกแล้ว และอัปเดตบนอุปกรณ์แล้ว" : "บันทึกแล้ว");
            BreezAudio.Play("result_calm", 0.6f);
        });
    }

    // -----------------------------------------------------------------
    void RefreshEdits()
    {
        Color c = DeviceInfo.ParseColor(editHex);
        Sprite sprite = null;
        if (iconOptions != null)
            foreach (var opt in iconOptions)
            {
                bool sel = opt.id == editIcon;
                if (opt.frame != null) opt.frame.color = sel ? optionSelected : optionNormal;
                if (opt.icon != null) opt.icon.color = sel ? c : new Color32(120, 120, 120, 255);
                if (sel) sprite = opt.sprite;
            }
        if (iconImage != null)
        {
            if (sprite != null) iconImage.sprite = sprite;
            iconImage.color = c;
        }
        if (iconBackground != null) iconBackground.color = Color.Lerp(c, Color.white, 0.82f);
        if (colorOptions != null)
            foreach (var opt in colorOptions)
            {
                bool sel = string.Equals(opt.hex, editHex, StringComparison.OrdinalIgnoreCase);
                if (opt.ring != null) { opt.ring.enabled = sel; opt.ring.color = DeviceInfo.ParseColor(opt.hex); }
                if (opt.button != null) opt.button.transform.localScale = sel ? Vector3.one * 1.08f : Vector3.one;
            }
        if (previewName != null) previewName.text = string.IsNullOrEmpty(editName) ? "Breath Gadget" : editName;

        bool dirty = Dirty;
        if (saveButton != null) saveButton.interactable = dirty && !saving;
        if (saveLabel != null) saveLabel.text = saving ? "กำลังบันทึก..." : dirty ? "บันทึก" : "บันทึกแล้ว";
        if (resetButton != null) resetButton.gameObject.SetActive(dirty && !saving);
    }

    string Clean(string v)
    {
        string name = (v ?? "").Trim();
        return name.Length > maxNameLength ? name.Substring(0, maxNameLength) : name;
    }

    void UpdateStatus()
    {
        bool connected = DeviceService.StatusNow == "connected" || DeviceService.ServerSaysConnected;
        if (statusText != null) statusText.text = connected ? "เชื่อมต่ออยู่" : "ยังไม่เชื่อมต่อ";
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
        toastUntil = Time.unscaledTime + 3.5f;
    }
}
