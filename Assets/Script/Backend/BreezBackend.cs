using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// =====================================================================
// BreezBackend: ตัวกลางคุยกับ Supabase (Auth + RPC) ผ่าน UnityWebRequest
// - สร้างตัวเองอัตโนมัติตอนเปิดเกม และอยู่ข้าม scene (ไม่ต้องลากใส่ scene)
// - จำการ login ไว้ใน PlayerPrefs เปิดเกมครั้งต่อไปไม่ต้อง login ใหม่
// - ถ้าเน็ตหลุด ผลการเล่นจะถูกเก็บในคิว แล้วส่งให้อัตโนมัติเมื่อกลับมาออนไลน์
// =====================================================================

[Serializable] public class AuthUserMeta { public string name; }
[Serializable] public class AuthUser { public string id; public string email; public AuthUserMeta user_metadata; }

[Serializable]
public class AuthSession
{
    public string access_token;
    public string refresh_token;
    public int expires_in;
    public long expires_at;
    public AuthUser user;
}

[Serializable] class ApiError { public string error; public string error_description; public string msg; public string message; public string error_code; }
[Serializable] class PendingCall { public string user; public string fn; public string body; }
[Serializable] class PendingQueue { public List<PendingCall> items = new List<PendingCall>(); }

public class BreezBackend : MonoBehaviour
{
    const string PrefSession = "breez.session";
    const string PrefQueue = "breez.queue";

    static BreezBackend instance;
    public static BreezBackend Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("[BreezBackend]");
                instance = go.AddComponent<BreezBackend>();
            }
            return instance;
        }
    }

    /// <summary>คืน instance ที่มีอยู่ โดยไม่สร้างใหม่ (ใช้ใน OnDestroy)</summary>
    public static BreezBackend Existing => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() { _ = Instance; }

    public AuthSession Session { get; private set; }
    public bool IsLoggedIn => Session != null && !string.IsNullOrEmpty(Session.refresh_token);
    public string UserId => Session?.user?.id;
    public string Email => Session?.user?.email;
    public string DisplayName
    {
        get
        {
            string n = Session?.user?.user_metadata?.name;
            if (!string.IsNullOrEmpty(n)) return n;
            return string.IsNullOrEmpty(Email) ? "" : Email.Split('@')[0];
        }
    }

    /// <summary>true = เพิ่ง login, false = เพิ่ง logout</summary>
    public event Action<bool> LoginStateChanged;

    PendingQueue queue = new PendingQueue();
    bool refreshing;
    bool flushing;
    string lastSentStatus;
    int? lastSentBattery;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadState();
    }

    void Start()
    {
        StartCoroutine(HeartbeatLoop());
    }

    // =================================================================
    // AUTH
    // =================================================================

    public void SignUp(string email, string password, string displayName, Action<bool, string> done)
    {
        string body = Json.Obj(
            "email", Json.Str(email.Trim()),
            "password", Json.Str(password),
            "data", Json.Obj("name", Json.Str(displayName.Trim())));
        StartCoroutine(AuthCo("/auth/v1/signup", body, done, isSignUp: true));
    }

    public void SignIn(string email, string password, Action<bool, string> done)
    {
        string body = Json.Obj("email", Json.Str(email.Trim()), "password", Json.Str(password));
        StartCoroutine(AuthCo("/auth/v1/token?grant_type=password", body, done, isSignUp: false));
    }

    public void SignOut()
    {
        if (Session != null)
        {
            // แจ้งอุปกรณ์ว่าตัดการเชื่อมต่อ แล้ว revoke token (ไม่รอผล)
            string token = Session.access_token;
            StartCoroutine(Send("POST", "/auth/v1/logout", "{}", token, null));
        }
        Session = null;
        lastSentStatus = null;
        lastSentBattery = null;
        SaveSession();
        LoginStateChanged?.Invoke(false);
    }

    IEnumerator AuthCo(string path, string body, Action<bool, string> done, bool isSignUp)
    {
        long code = 0; string text = null;
        yield return Send("POST", path, body, null, (c, t) => { code = c; text = t; });

        if (code < 200 || code >= 300)
        {
            done?.Invoke(false, FriendlyError(text, code));
            yield break;
        }

        AuthSession s = null;
        try { s = JsonUtility.FromJson<AuthSession>(text); } catch { /* ตรวจด้านล่าง */ }

        if (s == null || string.IsNullOrEmpty(s.access_token))
        {
            // สมัครสำเร็จแต่ project เปิด "Confirm email" อยู่
            done?.Invoke(isSignUp, isSignUp
                ? "สร้างบัญชีแล้ว กรุณายืนยันอีเมลก่อนเข้าสู่ระบบ"
                : "เข้าสู่ระบบไม่สำเร็จ กรุณาลองอีกครั้ง");
            yield break;
        }

        SetSession(s);
        done?.Invoke(true, isSignUp ? "สร้างบัญชีสำเร็จ!" : "เข้าสู่ระบบแล้ว!");
        LoginStateChanged?.Invoke(true);
        DeviceService.Sync(DeviceService.LastBattery, DeviceService.StatusNow);
        StartCoroutine(FlushQueue());
    }

    void SetSession(AuthSession s)
    {
        if (s.expires_at <= 0 && s.expires_in > 0)
            s.expires_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + s.expires_in;
        Session = s;
        SaveSession();
    }

    bool TokenExpiringSoon =>
        Session == null || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > Session.expires_at - 60;

    IEnumerator EnsureFreshToken()
    {
        while (refreshing) yield return null;
        if (IsLoggedIn && TokenExpiringSoon) yield return RefreshCo();
    }

    IEnumerator RefreshCo()
    {
        if (refreshing) { while (refreshing) yield return null; yield break; }
        if (!IsLoggedIn) yield break;
        refreshing = true;

        long code = 0; string text = null;
        string body = Json.Obj("refresh_token", Json.Str(Session.refresh_token));
        yield return Send("POST", "/auth/v1/token?grant_type=refresh_token", body, null, (c, t) => { code = c; text = t; });

        if (code >= 200 && code < 300)
        {
            var s = JsonUtility.FromJson<AuthSession>(text);
            if (s != null && !string.IsNullOrEmpty(s.access_token)) SetSession(s);
        }
        else if (code == 400 || code == 401 || code == 403)
        {
            // refresh token หมดอายุ/ถูกยกเลิก => ต้อง login ใหม่
            Debug.LogWarning("[Breez] Session expired, please sign in again.");
            Session = null;
            SaveSession();
            LoginStateChanged?.Invoke(false);
        }
        // code 0 = ออฟไลน์ เก็บ session เดิมไว้ก่อน

        refreshing = false;
    }

    // =================================================================
    // RPC (เรียกฟังก์ชันใน Supabase)
    // =================================================================

    /// <summary>
    /// เรียก POST /rest/v1/rpc/{fn}
    /// queueOnFailure = true : ถ้าส่งไม่ได้เพราะเน็ต/เซิร์ฟเวอร์ จะเก็บไว้ส่งใหม่อัตโนมัติ
    /// </summary>
    public void Rpc(string fn, string jsonBody, Action<bool, string> done = null, bool queueOnFailure = false)
    {
        StartCoroutine(RpcCo(fn, jsonBody ?? "{}", done, queueOnFailure));
    }

    IEnumerator RpcCo(string fn, string body, Action<bool, string> done, bool queueOnFailure)
    {
        if (!IsLoggedIn)
        {
            done?.Invoke(false, "ยังไม่ได้เข้าสู่ระบบ");
            yield break;
        }
        string owner = UserId;

        long code = 0; string text = null;
        yield return CallOnce(fn, body, (c, t) => { code = c; text = t; });

        bool ok = code >= 200 && code < 300;
        if (!ok && queueOnFailure && (code == 0 || code >= 500 || code == 401))
        {
            Enqueue(owner, fn, body);
            done?.Invoke(false, "บันทึกแบบออฟไลน์ จะส่งเมื่อออนไลน์");
            yield break;
        }

        done?.Invoke(ok, ok ? text : FriendlyError(text, code));
        if (ok && queue.items.Count > 0) StartCoroutine(FlushQueue());
    }

    IEnumerator CallOnce(string fn, string body, Action<long, string> onDone)
    {
        yield return EnsureFreshToken();
        if (!IsLoggedIn) { onDone(401, "ยังไม่ได้เข้าสู่ระบบ"); yield break; }

        long code = 0; string text = null;
        yield return Send("POST", "/rest/v1/rpc/" + fn, body, Session.access_token, (c, t) => { code = c; text = t; });

        if (code == 401 && IsLoggedIn)
        {
            yield return RefreshCo();
            if (IsLoggedIn)
                yield return Send("POST", "/rest/v1/rpc/" + fn, body, Session.access_token, (c, t) => { code = c; text = t; });
        }
        onDone(code, text);
    }

    // =================================================================
    // OFFLINE QUEUE
    // =================================================================

    void Enqueue(string user, string fn, string body)
    {
        queue.items.Add(new PendingCall { user = user, fn = fn, body = body });
        if (queue.items.Count > 200) queue.items.RemoveAt(0);
        SaveQueue();
        Debug.Log($"[Breez] Queued {fn} (offline). Pending: {queue.items.Count}");
    }

    public int PendingCount => queue.items.Count;

    IEnumerator FlushQueue()
    {
        if (flushing || !IsLoggedIn) yield break;
        flushing = true;

        for (int i = 0; i < queue.items.Count;)
        {
            var item = queue.items[i];
            if (item.user != UserId) { i++; continue; } // ส่งเฉพาะของผู้ใช้ที่ login อยู่

            long code = 0;
            yield return CallOnce(item.fn, item.body, (c, t) => { code = c; });

            if (code >= 200 && code < 300) { queue.items.RemoveAt(i); SaveQueue(); }
            else if (code == 0 || code >= 500 || code == 401) break; // ยังออฟไลน์ ลองใหม่รอบหน้า
            else { Debug.LogWarning($"[Breez] Dropped invalid queued call {item.fn} ({code})"); queue.items.RemoveAt(i); SaveQueue(); }
        }

        flushing = false;
    }

    // =================================================================
    // DEVICE HEARTBEAT
    // =================================================================

    IEnumerator HeartbeatLoop()
    {
        var wait = new WaitForSecondsRealtime(5f);
        float nextBeat = 0f;
        while (true)
        {
            yield return wait;
            if (!IsLoggedIn) continue;

            string status = DeviceService.StatusNow;
            int? battery = DeviceService.LastBattery;
            bool changed = status != lastSentStatus || battery != lastSentBattery;
            bool due = Time.realtimeSinceStartup >= nextBeat && status == "connected";

            if (changed || due)
            {
                lastSentStatus = status;
                lastSentBattery = battery;
                nextBeat = Time.realtimeSinceStartup + BreezConfig.DeviceHeartbeatSeconds;
                DeviceService.Sync(battery, status);
                if (queue.items.Count > 0) StartCoroutine(FlushQueue());
            }
        }
    }

    // =================================================================
    // HTTP + STORAGE
    // =================================================================

    IEnumerator Send(string method, string path, string body, string bearer, Action<long, string> onDone)
    {
        using (var req = new UnityWebRequest(BreezConfig.SupabaseUrl + path, method))
        {
            if (body != null) req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", BreezConfig.PublishableKey);
            if (!string.IsNullOrEmpty(bearer)) req.SetRequestHeader("Authorization", "Bearer " + bearer);
            req.timeout = 15;

            yield return req.SendWebRequest();

            long code = req.result == UnityWebRequest.Result.ConnectionError ? 0 : req.responseCode;
            string text = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (code < 200 || code >= 300) Debug.LogWarning($"[Breez] {method} {path} -> {code} {text}");
            onDone?.Invoke(code, text);
        }
    }

    static string FriendlyError(string text, long code)
    {
        if (code == 0) return "ไม่มีการเชื่อมต่ออินเทอร์เน็ต";
        string msg = null;
        try
        {
            var e = JsonUtility.FromJson<ApiError>(text);
            // JsonUtility ให้ค่า "" กับ field ที่ไม่มี จึงต้องเช็คค่าว่าง
            if (e != null)
                foreach (var m in new[] { e.msg, e.message, e.error_description, e.error })
                    if (!string.IsNullOrEmpty(m)) { msg = m; break; }
        }
        catch { /* ใช้ข้อความ default */ }

        if (string.IsNullOrEmpty(msg)) return $"เซิร์ฟเวอร์ขัดข้อง ({code})";
        if (msg.Contains("Invalid login credentials")) return "อีเมลหรือรหัสผ่านไม่ถูกต้อง";
        if (msg.Contains("already registered")) return "อีเมลนี้สมัครไว้แล้ว กรุณาเข้าสู่ระบบ";
        if (msg.Contains("Email not confirmed")) return "กรุณายืนยันอีเมลก่อน";
        if (msg.Contains("Unable to validate email")) return "กรุณากรอกอีเมลให้ถูกต้อง";
        return msg;
    }

    void LoadState()
    {
        try
        {
            string s = PlayerPrefs.GetString(PrefSession, "");
            if (!string.IsNullOrEmpty(s)) Session = JsonUtility.FromJson<AuthSession>(s);
            string q = PlayerPrefs.GetString(PrefQueue, "");
            if (!string.IsNullOrEmpty(q)) queue = JsonUtility.FromJson<PendingQueue>(q) ?? new PendingQueue();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Breez] Could not read saved state: " + e.Message);
            Session = null;
            queue = new PendingQueue();
        }
    }

    void SaveSession()
    {
        PlayerPrefs.SetString(PrefSession, Session != null ? JsonUtility.ToJson(Session) : "");
        PlayerPrefs.Save();
    }

    void SaveQueue()
    {
        PlayerPrefs.SetString(PrefQueue, JsonUtility.ToJson(queue));
        PlayerPrefs.Save();
    }
}
