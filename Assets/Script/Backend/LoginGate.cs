using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// =====================================================================
// บังคับ login ก่อนเล่น
//   • เปิดหน้าไหนก็ตาม (ยกเว้นหน้า Login) ถ้ายังไม่ได้ login => กลับไปหน้า Login ทันที
//   • ออกจากระบบ / session หมดอายุระหว่างเล่น => กลับไปหน้า Login
//   • ทำงานอัตโนมัติทุก scene ไม่ต้องใส่ component เพิ่ม
//   • login ค้างไว้ในเครื่อง (PlayerPrefs) เปิดเกมครั้งต่อไปไม่ต้อง login ใหม่ และเล่นออฟไลน์ได้
// ปิดได้ที่ BreezConfig.RequireLogin
// =====================================================================
public static class LoginGate
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!BreezConfig.RequireLogin) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        BreezBackend.Instance.LoginStateChanged -= OnLoginStateChanged;
        BreezBackend.Instance.LoginStateChanged += OnLoginStateChanged;
        Check(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Check(scene);
    }

    static void OnLoginStateChanged(bool loggedIn)
    {
        if (!loggedIn && !IsLoginScene(SceneManager.GetActiveScene())) GoToLogin();
    }

    static void Check(Scene scene)
    {
        if (IsLoginScene(scene) || BreezBackend.Instance.IsLoggedIn) return;
        Debug.Log($"[Breez] ต้อง login ก่อนเล่น => กลับหน้า Login (จาก {scene.name})");
        GoToLogin();
    }

    static bool IsLoginScene(Scene scene) =>
        string.Equals(scene.name, BreezConfig.LoginScene, StringComparison.OrdinalIgnoreCase);

    public static void GoToLogin()
    {
        Time.timeScale = 1f;   // บางเกมหยุดเวลาไว้ (หน้าวิธีการเล่น / ผลลัพธ์)
        int index = LoginBuildIndex();
        if (index >= 0) SceneManager.LoadScene(index);
        else Debug.LogError("[Breez] ไม่พบ scene Login ใน Build Settings (File > Build Settings)");
    }

    // หา scene Login ใน Build Settings แบบไม่สนตัวพิมพ์เล็ก/ใหญ่ (ไฟล์ชื่อ login.unity)
    static int LoginBuildIndex()
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string name = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
            if (string.Equals(name, BreezConfig.LoginScene, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }
}
