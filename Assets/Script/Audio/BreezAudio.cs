using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================
// ระบบเสียงกลางของ Breez (สร้างตัวเองอัตโนมัติ ไม่ต้องวางใน scene)
//   • เพลงประกอบ: เปลี่ยนตาม scene แบบค่อยๆ เฟด (ตั้งใน BreezSoundBank.sceneMusic)
//     scene ที่มี SPGameManager (Soundscape) จัดการเสียงของตัวเอง => หยุดเพลงกลาง
//   • เสียงคลิก: ใส่ให้ทุกปุ่มในทุก scene อัตโนมัติ (ปุ่มชื่อ Back / กลับ ใช้เสียง ui_back)
//     ไม่อยากให้ปุ่มไหนมีเสียง => ใส่ component BreezNoClickSound
//   • เสียงเอฟเฟกต์: BreezAudio.Play("balloon_pop")
//   • ระดับเสียง: BreezAudio.MusicVolume / SfxVolume / Muted (จำไว้ใน PlayerPrefs)
// =====================================================================
public class BreezAudio : MonoBehaviour
{
    const string BankPath = "BreezSoundBank";
    const string KMusic = "breez_music_volume", KSfx = "breez_sfx_volume", KMute = "breez_muted";

    static BreezAudio inst;
    BreezSoundBank bank;
    AudioSource musicA, musicB, current;
    readonly List<AudioSource> pool = new List<AudioSource>();
    int next;
    string currentMusic;
    float currentMusicVolume = 1f;
    Coroutine fade;
    readonly HashSet<int> hooked = new HashSet<int>();

    // ---------------- ระดับเสียง ----------------
    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(KMusic, 0.6f);
        set { PlayerPrefs.SetFloat(KMusic, Mathf.Clamp01(value)); if (inst != null) inst.ApplyMusicVolume(); }
    }
    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(KSfx, 0.9f);
        set => PlayerPrefs.SetFloat(KSfx, Mathf.Clamp01(value));
    }
    public static bool Muted
    {
        get => PlayerPrefs.GetInt(KMute, 0) == 1;
        set { PlayerPrefs.SetInt(KMute, value ? 1 : 0); if (inst != null) inst.ApplyMusicVolume(); }
    }
    /// <summary>ตัวคูณสำหรับ AudioSource อื่นในเกม (เช่น เสียงฝนของ Soundscape)</summary>
    public static float MusicScale => Muted ? 0f : MusicVolume;
    public static float SfxScale => Muted ? 0f : SfxVolume;

    // ---------------- เริ่มทำงาน ----------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => Ensure();

    public static BreezAudio Ensure()
    {
        if (inst != null) return inst;
        var go = new GameObject("BreezAudio");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<BreezAudio>();
        return inst;
    }

    void Awake()
    {
        if (inst != null && inst != this) { Destroy(gameObject); return; }
        inst = this;
        bank = Resources.Load<BreezSoundBank>(BankPath);
        musicA = NewSource(true); musicB = NewSource(true);
        for (int i = 0; i < 10; i++) pool.Add(NewSource(false));
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        if (inst == this) { SceneManager.sceneLoaded -= OnSceneLoaded; inst = null; }
    }

    AudioSource NewSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        s.ignoreListenerPause = true;
        return s;
    }

    int lastScene = -1;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || scene.handle == lastScene) return;
        lastScene = scene.handle;
        hooked.Clear();
        HookButtons(scene);
        PlayMusic(MusicIdFor(scene));
    }

    string MusicIdFor(Scene scene)
    {
        if (bank == null) return null;
        var id = bank.MusicFor(scene.name);
        if (id != null) return id;
        // Soundscape มีเสียงของตัวเอง (ฝน / ลม)
        if (FindObjectOfType<SPGameManager>() != null) return "";
        return bank.defaultMusic;
    }

    // ---------------- เพลง ----------------
    public static void PlayMusic(string id)
    {
        var a = Ensure();
        if (id == a.currentMusic) return;
        a.currentMusic = id;
        var snd = a.bank != null ? a.bank.Find(id) : null;
        a.currentMusicVolume = snd != null ? snd.volume : 1f;
        if (a.fade != null) a.StopCoroutine(a.fade);
        a.fade = a.StartCoroutine(a.CrossFade(snd != null ? snd.clip : null));
    }

    public static void StopMusic() => PlayMusic("");

    IEnumerator CrossFade(AudioClip clip)
    {
        var from = current;
        var to = clip != null ? (current == musicA ? musicB : musicA) : null;
        if (to != null) { to.clip = clip; to.volume = 0f; to.time = 0f; to.Play(); }
        float target = currentMusicVolume * MusicScale;
        float fromStart = from != null ? from.volume : 0f;
        const float dur = 1.2f;
        for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur;
            if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
            if (to != null) to.volume = Mathf.Lerp(0f, target, k);
            yield return null;
        }
        if (from != null) { from.volume = 0f; from.Stop(); }
        if (to != null) to.volume = target;
        current = to;
        fade = null;
    }

    void ApplyMusicVolume()
    {
        if (current != null && fade == null) current.volume = currentMusicVolume * MusicScale;
    }

    // ---------------- เสียงเอฟเฟกต์ ----------------
    public static AudioClip Clip(string id)
    {
        var a = Ensure();
        var s = a.bank != null ? a.bank.Find(id) : null;
        return s != null ? s.clip : null;
    }

    public static void Play(string id, float volume = 1f, float pitch = 1f)
    {
        var a = Ensure();
        var s = a.bank != null ? a.bank.Find(id) : null;
        if (s == null || s.clip == null || SfxScale <= 0f) return;
        var src = a.pool[a.next++ % a.pool.Count];
        src.pitch = pitch;
        src.PlayOneShot(s.clip, s.volume * volume * SfxScale);
    }

    /// <summary>เล่นหลังผ่านไป delay วินาที (นับเวลาจริง แม้เกมหยุดเวลาไว้)</summary>
    public static void PlayDelayed(string id, float delay, float volume = 1f)
    {
        var a = Ensure();
        a.StartCoroutine(a.Delayed(id, delay, volume));
    }

    IEnumerator Delayed(string id, float delay, float volume)
    {
        yield return new WaitForSecondsRealtime(delay);
        Play(id, volume);
    }

    // ---------------- เสียงคลิกปุ่ม ----------------
    /// <summary>ใส่เสียงคลิกให้ปุ่มทั้งหมดใต้ root (ใช้กับ UI ที่สร้างตอนเล่น)</summary>
    public static void HookButtons(GameObject root)
    {
        var a = Ensure();
        foreach (var b in root.GetComponentsInChildren<Button>(true)) a.Hook(b);
    }

    void HookButtons(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var b in root.GetComponentsInChildren<Button>(true)) Hook(b);
    }

    void Hook(Button b)
    {
        if (b == null || !hooked.Add(b.GetInstanceID())) return;
        if (b.GetComponent<BreezNoClickSound>() != null) return;
        string id = IsBack(b) ? "ui_back" : "ui_click";
        b.onClick.AddListener(() => Play(id));
    }

    static bool IsBack(Button b)
    {
        string n = b.name.ToLowerInvariant();
        if (n.Contains("back") || n.Contains("close")) return true;
        var t = b.GetComponentInChildren<TMPro.TMP_Text>(true);
        return t != null && (t.text.Contains("กลับ") || t.text.Trim() == "Back");
    }
}
