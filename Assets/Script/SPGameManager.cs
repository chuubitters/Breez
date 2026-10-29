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

        // เล่น BGM
        if (bgm != null)
            bgm.Play();

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
            if (countdownUI != null)
                countdownUI.Tick(count, count / (float)total, (count - 1) / (float)total, 1f);
            else if (countdownText != null)
                countdownText.text = count.ToString();

            yield return new WaitForSeconds(1f);

            count--;
        }

        // แสดง เริ่ม!
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
            PlaySound(inhaleSound);

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
            PlaySound(exhaleSound);

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
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}