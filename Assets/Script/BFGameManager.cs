using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =====================================================================
// Breathe & Focus: ผึ้งเก็บดอกไม้
//   เป่า = ผึ้งลอยขึ้น, หยุดเป่า = ผึ้งลดระดับ เก็บดอกไม้ให้ได้มากที่สุดภายในเวลาที่กำหนด
//   ดอกไม้ 3 สี คะแนนต่างกัน (ตั้งค่าใน FlowerSpawner)
//   หมดเวลา => หน้าผลลัพธ์: คะแนน / เวลา / ดอกไม้ทั้งหมด + จำนวนแต่ละสี (มีแอนิเมชัน)
// =====================================================================
public class BFGameManager : MonoBehaviour
{
    public static BFGameManager instance;

    [Header("Start UI")]
    public GameObject startPanel;
    public Button startButton;
    public TMP_Text countdownText;

    [Header("Gameplay UI")]
    public TMP_Text timeText;
    public TMP_Text scoreText;

    [Header("Summary UI (แบบเก่า)")]
    public GameObject gameOverPanel;
    public TMP_Text summaryTimeText;
    public TMP_Text summaryScoreText;
    public TMP_Text summaryObstacleText;

    [Header("หน้าผลลัพธ์")]
    public TMP_Text resultScoreText;      // คะแนนรวม
    public TMP_Text resultTimeText;       // เวลาที่เล่น
    public TMP_Text resultObstacleText;   // ดอกไม้ที่เก็บได้ทั้งหมด
    [Tooltip("จำนวนดอกแต่ละสี เรียงตาม FlowerSpawner.types")]
    public TMP_Text[] resultFlowerCounts;

    [Header("เก็บดอกไม้")]
    public FlowerSpawner spawner;
    [Tooltip("เวลาเล่นต่อรอบ (วินาที)")]
    public float gameDuration = 60f;

    [Header("Game Settings (แบบเก่า)")]
    public int scorePerObstacle = 10;

    private float gameTime = 0f;
    private int currentScore = 0;
    private int obstaclesPassed = 0;   // = ดอกไม้ที่เก็บได้ทั้งหมด
    private readonly Dictionary<string, int> flowerCounts = new Dictionary<string, int>();

    private bool isGameOver = false;
    private bool gameStarted = false;
    private bool isCountingDown = false;

    public bool CanCollect => gameStarted && !isGameOver;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        Time.timeScale = 0f;
        gameTime = 0f;
        currentScore = 0;
        obstaclesPassed = 0;
        flowerCounts.Clear();
        isGameOver = false;
        gameStarted = false;
        isCountingDown = false;

        if (spawner == null) spawner = FindObjectOfType<FlowerSpawner>();
        if (startPanel != null) startPanel.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartGame);
        }
        else Debug.LogError("Start Button is NOT assigned!");

        UpdateUI();
    }

    void Update()
    {
        if (!gameStarted || isGameOver) return;
        gameTime += Time.deltaTime;
        UpdateTimeUI();
        if (spawner != null && gameDuration > 0f && gameTime >= gameDuration)
        {
            gameTime = gameDuration;
            GameOver();
        }
    }

    public void StartGame()
    {
        if (gameStarted || isCountingDown) return;
        isCountingDown = true;
        if (startPanel != null) startPanel.SetActive(false);
        StartCoroutine(StartCountdown());
    }

    private IEnumerator StartCountdown()
    {
        if (countdownText != null) countdownText.gameObject.SetActive(true);
        foreach (var n in new[] { "3", "2", "1" })
        {
            if (countdownText != null) countdownText.text = n;
            BreezAudio.Play("count_tick");
            yield return new WaitForSecondsRealtime(1f);
        }
        if (countdownText != null) countdownText.text = "เริ่ม!";
        BreezAudio.Play("count_go");
        yield return new WaitForSecondsRealtime(0.5f);
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        gameStarted = true;
        isCountingDown = false;
        Time.timeScale = 1f;
        if (spawner != null) spawner.Begin();
    }

    // เก็บดอกไม้ (เรียกจาก FlowerPickup)
    public void CollectFlower(FlowerSpawner.FlowerType type)
    {
        if (!CanCollect) return;
        obstaclesPassed++;
        currentScore += type.points;
        flowerCounts.TryGetValue(type.id, out int n);
        flowerCounts[type.id] = n + 1;
        BreezAudio.Play("focus_point", 0.8f);
        UpdateUI();
    }

    // แบบเก่า (ผ่านสิ่งกีดขวาง) ยังใช้ได้ถ้าไม่มี FlowerSpawner
    public void AddScore()
    {
        if (!CanCollect || spawner != null) return;
        obstaclesPassed++;
        currentScore += scorePerObstacle;
        BreezAudio.Play("focus_point", 0.8f);
        UpdateUI();
    }

    void UpdateTimeUI()
    {
        if (timeText == null) return;
        float shown = spawner != null && gameDuration > 0f ? Mathf.Max(0f, gameDuration - gameTime) : gameTime;
        int s = spawner != null ? Mathf.CeilToInt(shown) : Mathf.FloorToInt(shown);
        timeText.text = string.Format("เวลา {0:00}:{1:00}", s / 60, s % 60);
    }

    void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = "คะแนน " + currentScore;
        UpdateTimeUI();
    }

    public int CountOf(string id) => flowerCounts.TryGetValue(id, out int n) ? n : 0;

    // จบเกม (หมดเวลา หรือแบบเก่า: ชนสิ่งกีดขวาง)
    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        gameStarted = false;
        if (spawner != null) spawner.Stop();
        else BreezAudio.Play("focus_hit");
        BreezAudio.PlayDelayed("result_end", 0.6f);

        int minutes = Mathf.FloorToInt(gameTime / 60f);
        int seconds = Mathf.FloorToInt(gameTime % 60f);
        string unit = spawner != null ? "ดอก" : "ชิ้น";

        if (summaryTimeText != null) summaryTimeText.text = string.Format("เวลา {0:00}:{1:00}", minutes, seconds);
        if (summaryScoreText != null) summaryScoreText.text = "คะแนนรวม " + currentScore;
        if (summaryObstacleText != null)
            summaryObstacleText.text = spawner != null ? $"เก็บดอกไม้ได้ {obstaclesPassed} ดอก" : $"หลบสิ่งกีดขวางได้ {obstaclesPassed} ชิ้น";
        if (resultTimeText != null) resultTimeText.text = $"{minutes:00}:{seconds:00}<size=40%>  นาที</size>";

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Time.timeScale = 0f;
        StartCoroutine(CountUp(unit));

        int pink = 0, yellow = 0, purple = 0;
        if (spawner != null && spawner.types.Length >= 3)
        {
            pink = CountOf(spawner.types[0].id); yellow = CountOf(spawner.types[1].id); purple = CountOf(spawner.types[2].id);
        }
        GameDataRecorder.RecordBreatheFocus(currentScore, gameTime, obstaclesPassed, pink, yellow, purple);
    }

    // ตัวเลขหน้าผลลัพธ์นับขึ้นทีละน้อย (ทำงานตอน timeScale = 0)
    IEnumerator CountUp(string unit)
    {
        float d = 1.1f, t = 0f;
        int n = resultFlowerCounts != null ? resultFlowerCounts.Length : 0;
        while (true)
        {
            float k = Mathf.Clamp01(t / d);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            if (resultScoreText != null) resultScoreText.text = Mathf.RoundToInt(currentScore * e).ToString();
            if (resultObstacleText != null) resultObstacleText.text = $"{Mathf.RoundToInt(obstaclesPassed * e)}<size=40%>  {unit}</size>";
            for (int i = 0; i < n && spawner != null && i < spawner.types.Length; i++)
                if (resultFlowerCounts[i] != null) resultFlowerCounts[i].text = $"{Mathf.RoundToInt(CountOf(spawner.types[i].id) * e)}";
            if (k >= 1f) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
