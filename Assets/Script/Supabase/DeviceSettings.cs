using TMPro;
using UnityEngine;
using UnityEngine.UI;

// หน้า Setting: ตั้งชื่อและไอคอนของ Breath Gadget (บันทึกลงตาราง devices ใน Supabase)
// ฟิลด์และชื่อฟังก์ชันเหมือนเวอร์ชันเดิม ผูกปุ่มเดิมได้เลย
public class DeviceSettings : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField deviceNameInput;
    public Image deviceIconImage;
    public TMP_Text statusText;
    public TMP_Text batteryText;   // ไม่ใส่ก็ได้

    [Header("Icons")]
    public Sprite catIcon;
    public Sprite flowerIcon;
    public Sprite cloudIcon;
    public Sprite starIcon;

    private string selectedIcon = "cat";

    private void Start()
    {
        LoadDevice();
    }

    public void LoadDevice()
    {
        if (!BreezBackend.Instance.IsLoggedIn)
        {
            SetStatus("Not logged in");
            return;
        }

        SetStatus("Loading...");
        DeviceService.Sync(done: device =>
        {
            if (device == null) { SetStatus("Load failed"); return; }
            if (deviceNameInput != null) deviceNameInput.text = device.device_name;
            selectedIcon = string.IsNullOrEmpty(device.device_icon) ? "cat" : device.device_icon;
            UpdateIconUI(selectedIcon);
            if (batteryText != null)
                batteryText.text = DeviceService.LastBattery.HasValue ? DeviceService.LastBattery + "%" : "—";
            SetStatus("Loaded");
        });
    }

    public void SelectCat() { selectedIcon = "cat"; UpdateIconUI(selectedIcon); }
    public void SelectFlower() { selectedIcon = "flower"; UpdateIconUI(selectedIcon); }
    public void SelectCloud() { selectedIcon = "cloud"; UpdateIconUI(selectedIcon); }
    public void SelectStar() { selectedIcon = "star"; UpdateIconUI(selectedIcon); }

    private void UpdateIconUI(string iconName)
    {
        if (deviceIconImage == null) return;
        switch (iconName)
        {
            case "flower": deviceIconImage.sprite = flowerIcon; break;
            case "cloud": deviceIconImage.sprite = cloudIcon; break;
            case "star": deviceIconImage.sprite = starIcon; break;
            default: deviceIconImage.sprite = catIcon; break;
        }
    }

    public void SaveDevice()
    {
        string newName = deviceNameInput != null ? deviceNameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(newName))
        {
            SetStatus("Please enter device name.");
            return;
        }

        SetStatus("Saving...");
        DeviceService.Sync(name: newName, icon: selectedIcon, done: device =>
        {
            SetStatus(device != null ? "Saved" : "Save failed");
        });
    }

    private void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
    }
}
