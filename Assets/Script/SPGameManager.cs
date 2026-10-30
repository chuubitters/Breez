using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SPGameManager : MonoBehaviour
{
    [Header("Breathing Settings")]
    public float inhaleDuration = 5f;
    public float exhaleDuration = 5f;

    [Header("Breath Input")]
    public BreathReceiver breathReceiver;

    [Range(0, 100)]
    public int breathThreshold = 50;

    [Header("Start Panel")]
    public GameObject startPanel;
    public Button startButton;

    [Header("Countdown UI")]
    public TMP_Text countdownText;
    public float countdownTime = 3f;

    [Header("UI")]
    public TMP_Text breathingText;
    public Slider breathingBar;

    [Header("UI ใหม่ (Breez > Restyle Soundscape Countdown)")]
    public SoundscapeCountdownUI countdownUI;
    public BreathGuideUI breathGuide;

    [Header("จบการฝึก + หน้าผลลัพธ์")]
    [Tooltip("ปุ่ม \"จบการฝึก\" (ผูกให้อัตโนมัติ)")]
    public Button endButton;
    public SoundscapeResultUI resultUI;

    [Header("Breathing Audio")]
    public AudioSource audioSource;
    public AudioClip inhaleSound;
    public AudioClip exhaleSound;

    [Header("Rain & Input Event")]
    public AudioSource bgm;
    public AudioSource sfx;
    public ParticleSystem rainParticles;
    [Tooltip("ถ้าไม่มี Rain Particles ใน scene ที่ชื่อมีคำว่า Rain ให้สร้างฝนให้อัตโนมัติ")]
    public bool autoCreateRain = true;

    private bool isBreathing = false;
    private bool gameStarted = false;

    /// <summary>เริ่มเล่นแล้ว (ผ่าน countdown) — ให้ effect อื่นๆ เช่น ฝน / แดนดิไลออน ใช้เช็ค</summary>
    public bool IsPlaying => gameStarted;
    private bool wasInputActive = false;

    [Header("Save Session")]
    [Tooltip("บันทึกเป็น Soundscape session เมื่อฝึกนานอย่างน้อยกี่วินาที")]
    public float minSecondsToSave = 10f;
    private float sessionStartTime;
    private bool sessionSaved = false;
    private int breathCycles = 0;       // จำนวนรอบ เข้า-ออก ที่ครบ
    private float exhaleSeconds = 0f;   // เวลาที่เป่า (เซนเซอร์/ลูกศร) รวม

    private void Start()
    {
        // ตั้งค่า Breathing Bar
        if (breathingBar != null)
        {
            breathingBar.minValue = 0f;
            breathingBar.maxValue = 1f;
            breathingBar.value = 0f;
        }

        SetupAudio();

        // เล่น BGM
        if (bgm != null)
            bgm.Play();

        // ยังไม่ได้ผูกในฉาก => หาเอง
        if (resultUI == null) resultUI = FindObjectOfType<SoundscapeResultUI>(true);
        if (endButton == null)
            foreach (var b in FindObjectsOfType<Button>(true))
            {
                var im = b.GetComponent<Image>();
                string n = b.name.ToLower() + " " + (im != null && im.sprite != null ? im.sprite.name.ToLower() : "");
                if (n.Contains("end")) { endButton = b; break; }
            }

        if (endButton != null)
        {
            endButton.onClick.RemoveListener(EndSession);
            endButton.onClick.AddListener(EndSession);
        }
        if (resultUI != null)
            resultUI.gameObject.SetActive(false);

        // ฝนหายไปจาก scene (เช่น ถูกลบ) => สร้างใหม่ให้อัตโนมัติ
        if (rainParticles == null && autoCreateRain && gameObject.scene.name.ToLower().Contains("rain"))
            rainParticles = BreathRain.Create();

        // ปิดฝนก่อนเริ่มเกม
        if (rainParticles != null)
            rainParticles.Stop();

        // ซ่อน Countdown
        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        // แสดง Start Panel
        if (startPanel != null)
            startPanel.SetActive(true);

        // ตั้งค่า Start Button
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartGame);
        }

        // เกมยังไม่เริ่ม
        gameStarted = false;
    }

    private void Update()
    {
        // เสียงลม/ฝนตอนเป่า ดังตามแรงเป่า
        if (sfx != null && sfx.isPlaying)
        {
            float level = breathReceiver != null ? Mathf.InverseLerp(breathThreshold, 100f, breathReceiver.breathLevel) : 0f;
            if (Input.GetKey(KeyCode.UpArrow)) level = Mathf.Max(level, 0.7f);
            float target = sfxBaseVolume * Mathf.Lerp(0.55f, 1f, level) * BreezAudio.SfxScale;
            sfx.volume = Mathf.MoveTowards(sfx.volume, target, Time.deltaTime * 2f);
        }

        // ยังไม่เริ่มเกม ไม่รับ Input
        if (!gameStarted)
            return;

        // ==================================
        // 1. ลูกศรขึ้น = Input สำรอง
        // ==================================
        bool arrowInput = Input.GetKey(KeyCode.UpArrow);

        // ==================================
        // 2. เป่า = Input หลัก
        // ==================================
        bool breathInput = false;

        if (breathReceiver != null)
        {
            breathInput =
                breathReceiver.breathLevel >= breathThreshold;
        }

        // ==================================
        // 3. รวม Input
        // ==================================
        bool inputActive = arrowInput || breathInput;

        // เริ่ม Effect
        if (inputActive && !wasInputActive)
        {
            StartInputEffect();
        }

        // หยุด Effect
        if (!inputActive && wasInputActive)
        {
            StopInputEffect();
        }

        if (inputActive) exhaleSeconds += Time.deltaTime;

        wasInputActive = inputActive;
    }

    // ==================================
    // กดปุ่ม START
    // ==================================
    public void StartGame()
    {
        if (gameStarted)
            return;

        // ปิด Start Panel
        if (startPanel != null)
            startPanel.SetActive(false);

        // เริ่ม Countdown
        StartCoroutine(StartCountdown());
    }

    // ==================================
    // Countdown
    // ==================================
    private IEnumerator StartCountdown()
    {
        int total = Mathf.Max(1, Mathf.CeilToInt(countdownTime));
        int count = total;

        if (countdownUI != null)
            countdownUI.Show();
        else if (countdownText != null)
            countdownText.gameObject.SetActive(true);

        while (count > 0)
        {
            BreezAudio.Play("count_tick");
            if (countdownUI != null)
                countdownUI.Tick(count, count / (float)total, (count - 1) / (float)total, 1f);
            else if (countdownText != null)
                countdownText.text = count.ToString();

            yield return new WaitForSeconds(1f);

            count--;
        }

        // แสดง เริ่ม!
        BreezAudio.Play("count_go");
        if (countdownUI != null)
            countdownUI.Go();
        else if (countdownText != null)
            countdownText.text = "เริ่ม!";

        yield return new WaitForSeconds(0.6f);

        if (countdownUI != null)
            countdownUI.Hide();
        else if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        // เริ่มเกมจริง
        gameStarted = true;
        sessionStartTime = Time.realtimeSinceStartup;

        // เริ่มระบบหายใจ
        StartBreathing();

        Debug.Log("Breathing Game Started!");
    }

    // ==================================
    // เริ่ม Effect ตอนเป่า / กดลูกศร
    // ==================================
    private void StartInputEffect()
    {
        if (sfx != null)
            sfx.Play();

        if (rainParticles != null)
            rainParticles.Play();

        Debug.Log("Breath / Arrow Input ON");
    }

    // ==================================
    // หยุด Effect
    // ==================================
    private void StopInputEffect()
    {
        if (sfx != null)
            sfx.Stop();

        if (rainParticles != null)
            rainParticles.Stop();

        Debug.Log("Breath / Arrow Input OFF");
    }

    // ==================================
    // เริ่มระบบหายใจ
    // ==================================
    public void StartBreathing()
    {
        if (!isBreathing)
        {
            StartCoroutine(BreathingRoutine());
        }
    }

    private IEnumerator BreathingRoutine()
    {
        isBreathing = true;

        while (true)
        {
            // =========================
            // INHALE
            // =========================
            PlaySound(inhaleSound, "breath_in");

            yield return StartCoroutine(
                BreathingPhase(
                    "หายใจเข้า",
                    inhaleDuration,
                    0f,
                    1f
                )
            );

            // =========================
            // EXHALE
            // =========================
            PlaySound(exhaleSound, "breath_out");

            yield return StartCoroutine(
                BreathingPhase(
                    "หายใจออก",
                    exhaleDuration,
                    1f,
                    0f
                )
            );

            breathCycles++;
        }
    }

    // ==================================
    // Breathing Bar + Text
    // ==================================
    private IEnumerator BreathingPhase(
        string phaseName,
        float duration,
        float startValue,
        float endValue)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsed / duration);

            float remainingTime =
                duration - elapsed;

            int secondsLeft =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(remainingTime)
                );

            // ป้ายนำการหายใจแบบใหม่
            if (breathGuide != null)
            {
                breathGuide.SetPhase(startValue < endValue, secondsLeft, progress);
            }
            // Text แบบเดิม
            else if (breathingText != null)
            {
                breathingText.text =
                    phaseName + " " +
                    secondsLeft +
                    " วินาที";
            }

            // Slider
            if (breathingBar != null)
            {
                breathingBar.value =
                    Mathf.Lerp(
                        startValue,
                        endValue,
                        progress
                    );
            }

            yield return null;
        }

        if (breathingBar != null)
        {
            breathingBar.value = endValue;
        }
    }

    // ==================================
    // บันทึก session ตอนออกจาก scene / ปิดเกม
    // (เรียก SaveSession() จากปุ่ม Back ก็ได้)
    // ==================================
    public void SaveSession()
    {
        if (!gameStarted || sessionSaved)
            return;

        float seconds = Time.realtimeSinceStartup - sessionStartTime;
        if (seconds < minSecondsToSave)
            return;

        sessionSaved = true;
        GameDataRecorder.RecordSoundscape(seconds, gameObject.scene.name, breathCycles, exhaleSeconds);
    }

    // ==================================
    // กด "จบการฝึก" => หยุด บันทึก แล้วแสดงผลลัพธ์ (เวลาที่ใช้ในโหมดนี้)
    // ==================================
    public void EndSession()
    {
        if (sessionEnded) return;
        sessionEnded = true;

        float seconds = gameStarted ? Time.realtimeSinceStartup - sessionStartTime : 0f;
        SaveSession();

        StopAllCoroutines();
        isBreathing = false;
        if (wasInputActive) StopInputEffect();
        wasInputActive = false;
        gameStarted = false;
        if (countdownUI != null) countdownUI.Hide();
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (startPanel != null) startPanel.SetActive(false);
        if (breathGuide != null) breathGuide.gameObject.SetActive(false);
        if (endButton != null) endButton.interactable = false;

        BreezAudio.Play("result_calm");

        if (resultUI != null)
            resultUI.Show(seconds, breathCycles, exhaleSeconds, sessionSaved);
        else
            Debug.Log("Soundscape ended: " + seconds.ToString("0") + "s");
    }

    // ==================================
    // เสียง: ถ้าใน scene ยังไม่ได้ใส่เสียง ใช้เสียงของ Breez (Resources/BreezSoundBank)
    // ==================================
    private float sfxBaseVolume = 1f;
    private bool sessionEnded = false;

    private void SetupAudio()
    {
        bool windy = gameObject.scene.name.ToLower().Contains("wind");
        if (bgm != null)
        {
            if (bgm.clip == null) bgm.clip = BreezAudio.Clip(windy ? "bgm_windy_mountain" : "amb_rain");
            bgm.loop = true;
            bgm.playOnAwake = false;
            bgm.volume *= BreezAudio.MusicScale;
        }
        if (sfx != null)
        {
            if (sfx.clip == null) sfx.clip = BreezAudio.Clip(windy ? "amb_wind" : "amb_rain");
            sfx.loop = true;
            sfx.playOnAwake = false;
            sfxBaseVolume = sfx.volume;
        }
    }

    private void OnDestroy()
    {
        SaveSession();
    }

    private void OnApplicationQuit()
    {
        SaveSession();
    }

    // ==================================
    // เล่นเสียง
    // ==================================
    private void PlaySound(AudioClip clip, string fallbackId)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, BreezAudio.SfxScale);
        }
        else
        {
            // ไม่ได้ใส่เสียงหายใจเข้า/ออกไว้ => ใช้เสียงนำหายใจของ Breez
            BreezAudio.Play(fallbackId, 0.8f);
        }
    }
}