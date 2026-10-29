using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================
// หน้า Login / Create account ของผู้เล่น
// ใช้เมนู Breez > Create Login Scene เพื่อสร้าง scene พร้อมต่อสายให้อัตโนมัติ
// หรือจะลากใส่ UI ที่ออกแบบเองก็ได้ แค่ใส่ช่องต่างๆ ใน Inspector
// =====================================================================
public class LoginUI : MonoBehaviour
{
    [Header("Inputs")]
    public TMP_InputField nameInput;      // ใช้ตอนสมัครเท่านั้น
    [Tooltip("กลุ่มของช่องชื่อ (label + input) จะถูกซ่อนตอน Sign in — ไม่ใส่ก็ได้")]
    public GameObject nameGroup;
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;

    [Header("Buttons")]
    public Button submitButton;
    public TMP_Text submitLabel;
    public Button switchModeButton;
    public TMP_Text switchModeLabel;

    [Header("Texts")]
    public TMP_Text titleText;
    public TMP_Text statusText;

    [Header("Flow")]
    public string nextScene = BreezConfig.HomeScene;
    [Tooltip("ถ้าเคย login ไว้แล้ว ข้ามหน้านี้ไปเลย")]
    public bool skipIfLoggedIn = true;

    bool registerMode;
    bool busy;

    void Start()
    {
        if (skipIfLoggedIn && BreezBackend.Instance.IsLoggedIn)
        {
            GoNext();
            return;
        }

        if (passwordInput != null)
        {
            passwordInput.contentType = TMP_InputField.ContentType.Password;
            passwordInput.ForceLabelUpdate();
        }
        if (submitButton != null) { submitButton.onClick.RemoveListener(OnSubmit); submitButton.onClick.AddListener(OnSubmit); }
        if (switchModeButton != null) { switchModeButton.onClick.RemoveListener(ToggleMode); switchModeButton.onClick.AddListener(ToggleMode); }

        SetMode(false);
        SetStatus("", false);
    }

    void Update()
    {
        // Tab = ช่องถัดไป, Enter = ยืนยัน
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) OnSubmit();
        if (Input.GetKeyDown(KeyCode.Tab)) FocusNext();
    }

    public void ToggleMode() => SetMode(!registerMode);

    void SetMode(bool register)
    {
        registerMode = register;
        if (nameGroup != null) nameGroup.SetActive(register);
        else if (nameInput != null) nameInput.gameObject.SetActive(register);
        if (titleText != null) titleText.text = register ? "สร้างบัญชีใหม่" : "ยินดีต้อนรับกลับ";
        if (submitLabel != null) submitLabel.text = register ? "สมัครสมาชิก" : "เข้าสู่ระบบ";
        if (switchModeLabel != null) switchModeLabel.text = register ? "มีบัญชีแล้ว? เข้าสู่ระบบ" : "ยังไม่มีบัญชี? สมัครสมาชิก";
        SetStatus("", false);
    }

    public void OnSubmit()
    {
        if (busy) return;

        string email = emailInput != null ? emailInput.text.Trim() : "";
        string password = passwordInput != null ? passwordInput.text : "";
        string displayName = nameInput != null ? nameInput.text.Trim() : "";

        if (string.IsNullOrEmpty(email) || !email.Contains("@")) { SetStatus("กรุณากรอกอีเมลให้ถูกต้อง", true); return; }
        if (password.Length < 6) { SetStatus("รหัสผ่านต้องมีอย่างน้อย 6 ตัวอักษร", true); return; }
        if (registerMode && string.IsNullOrEmpty(displayName)) { SetStatus("กรุณากรอกชื่อของคุณ", true); return; }

        SetBusy(true);
        SetStatus(registerMode ? "กำลังสร้างบัญชี..." : "กำลังเข้าสู่ระบบ...", false);

        if (registerMode)
            BreezBackend.Instance.SignUp(email, password, displayName, OnAuthResult);
        else
            BreezBackend.Instance.SignIn(email, password, OnAuthResult);
    }

    void OnAuthResult(bool ok, string message)
    {
        SetBusy(false);
        SetStatus(message, !ok);
        if (ok && BreezBackend.Instance.IsLoggedIn) Invoke(nameof(GoNext), 0.4f);
        else if (ok) SetMode(false); // สมัครแล้วแต่ต้องยืนยันอีเมลก่อน
    }

    void GoNext()
    {
        if (!string.IsNullOrEmpty(nextScene)) SceneManager.LoadScene(nextScene);
    }

    void SetBusy(bool value)
    {
        busy = value;
        if (submitButton != null) submitButton.interactable = !value;
        if (switchModeButton != null) switchModeButton.interactable = !value;
    }

    void SetStatus(string text, bool isError)
    {
        if (statusText == null) return;
        statusText.text = text;
        statusText.color = isError ? new Color(0.78f, 0.36f, 0.29f) : new Color(0.36f, 0.52f, 0.32f);
    }

    void FocusNext()
    {
        TMP_InputField[] order = registerMode
            ? new[] { nameInput, emailInput, passwordInput }
            : new[] { emailInput, passwordInput };
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] != null && order[i].isFocused)
            {
                var next = order[(i + 1) % order.Length];
                if (next != null) next.Select();
                return;
            }
        }
        if (order[0] != null) order[0].Select();
    }
}
