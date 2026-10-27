using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// =====================================================================
// ใส่ใน scene ไหนก็ได้ (เช่น Home / Setting)
// - requireLogin: ถ้ายังไม่ login จะเด้งกลับหน้า Login
// - playerNameText: แสดงชื่อผู้เล่น เช่น "Hi, Pim"
// - ผูกปุ่ม Logout ไปที่ SignOut()
// =====================================================================
public class AccountActions : MonoBehaviour
{
    public bool requireLogin = true;
    public TMP_Text playerNameText;
    public string greetingFormat = "Hi, {0}";

    void Start()
    {
        var backend = BreezBackend.Instance;
        if (requireLogin && !backend.IsLoggedIn)
        {
            SceneManager.LoadScene(BreezConfig.LoginScene);
            return;
        }
        if (playerNameText != null) playerNameText.text = string.Format(greetingFormat, backend.DisplayName);
        backend.LoginStateChanged += OnLoginStateChanged;
    }

    void OnDestroy()
    {
        if (BreezBackend.Existing != null) BreezBackend.Existing.LoginStateChanged -= OnLoginStateChanged;
    }

    void OnLoginStateChanged(bool loggedIn)
    {
        // session หมดอายุระหว่างเล่น
        if (!loggedIn && requireLogin) SceneManager.LoadScene(BreezConfig.LoginScene);
    }

    public void SignOut()
    {
        BreezBackend.Instance.SignOut();
        SceneManager.LoadScene(BreezConfig.LoginScene);
    }
}
