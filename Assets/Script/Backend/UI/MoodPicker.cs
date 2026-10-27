using TMPro;
using UnityEngine;

// =====================================================================
// ปุ่มเลือกอารมณ์ (Mood Tracking)
// ผูกปุ่มแต่ละปุ่มใน OnClick ไปที่ SelectLovely / SelectHappy / ...
// หรือ SelectMood("Happy")
// =====================================================================
public class MoodPicker : MonoBehaviour
{
    public TMP_InputField noteInput;   // ไม่ใส่ก็ได้
    public TMP_Text statusText;        // ไม่ใส่ก็ได้
    public GameObject hideAfterSave;   // เช่น panel เลือกอารมณ์ ให้ปิดหลังบันทึก

    bool busy;

    public void SelectLovely() => SelectMood("Lovely");
    public void SelectHappy() => SelectMood("Happy");
    public void SelectBored() => SelectMood("Bored");
    public void SelectAngry() => SelectMood("Angry");
    public void SelectStress() => SelectMood("Stress");

    public void SelectMood(string mood)
    {
        if (busy) return;
        busy = true;
        if (statusText != null) statusText.text = "Saving...";

        string note = noteInput != null ? noteInput.text : null;
        GameDataRecorder.LogMood(mood, note, ok =>
        {
            busy = false;
            if (statusText != null) statusText.text = ok ? $"Feeling {mood} — saved" : "Saved offline, will sync later";
            if (noteInput != null) noteInput.text = "";
            if (hideAfterSave != null) hideAfterSave.SetActive(false);
        });
    }
}
