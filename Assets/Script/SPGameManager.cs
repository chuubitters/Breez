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

    [Header("Breathing Audio")]
    public AudioSource audioSource;
    public AudioClip inhaleSound;
    public AudioClip exhaleSound;

    [Header("Rain & Input Event")]
    public AudioSource bgm;
    public AudioSource sfx;
    public ParticleSystem rainParticles;

    private bool isBreathing = false;
    private bool gameStarted = false;
    private bool wasInputActive = false;

    [Header("Save Session")]
    [Tooltip("บันทึกเป็น Soundscape session เมื่อฝึกนานอย่างน้อยกี่วินาที")]
    public float minSecondsToSave = 10f;
    private float sessionStartTime;
    private bool sessionSaved = false;

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
        if (countdownText != null)
            countdownText.gameObject.SetActive(true);

        int count = Mathf.CeilToInt(countdownTime);

        while (count > 0)
        {
            if (countdownText != null)
                countdownText.text = count.ToString();

            yield return new WaitForSeconds(1f);

            count--;
        }

        // แสดง GO!
        if (countdownText != null)
            countdownText.text = "GO!";

        yield return new WaitForSeconds(0.5f);

        if (countdownText != null)
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
                    "Inhale",
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
                    "Exhale",
                    exhaleDuration,
                    1f,
                    0f
                )
            );
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

            // Text
            if (breathingText != null)
            {
                breathingText.text =
                    phaseName + " " +
                    secondsLeft +
                    " Second";
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
        GameDataRecorder.RecordSoundscape(seconds);
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