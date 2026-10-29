using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Summary UI")]
    public GameObject gameOverPanel;
    public TMP_Text summaryTimeText;
    public TMP_Text summaryScoreText;
    public TMP_Text summaryObstacleText;

    [Header("Game Settings")]
    public int scorePerObstacle = 10;

    private float gameTime = 0f;
    private int currentScore = 0;
    private int obstaclesPassed = 0;

    private bool isGameOver = false;
    private bool gameStarted = false;
    private bool isCountingDown = false;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Stop game at the beginning
        Time.timeScale = 0f;

        gameTime = 0f;
        currentScore = 0;
        obstaclesPassed = 0;

        isGameOver = false;
        gameStarted = false;
        isCountingDown = false;

        // Show start panel
        if (startPanel != null)
            startPanel.SetActive(true);

        // Hide game over panel
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        // Hide countdown
        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        // Automatically connect Start Button
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartGame);

            Debug.Log("Start Button connected.");
        }
        else
        {
            Debug.LogError("Start Button is NOT assigned!");
        }

        UpdateUI();
    }

    void Update()
    {
        if (!gameStarted || isGameOver)
            return;

        gameTime += Time.deltaTime;

        UpdateTimeUI();
    }

    // =========================================
    // START BUTTON
    // =========================================
    public void StartGame()
    {
        if (gameStarted || isCountingDown)
            return;

        Debug.Log("START BUTTON PRESSED!");

        isCountingDown = true;

        if (startPanel != null)
            startPanel.SetActive(false);

        StartCoroutine(StartCountdown());
    }

    // =========================================
    // COUNTDOWN 3 -> 2 -> 1 -> GO!
    // =========================================
    private IEnumerator StartCountdown()
    {
        if (countdownText != null)
            countdownText.gameObject.SetActive(true);

        // 3
        if (countdownText != null)
            countdownText.text = "3";

        yield return new WaitForSecondsRealtime(1f);

        // 2
        if (countdownText != null)
            countdownText.text = "2";

        yield return new WaitForSecondsRealtime(1f);

        // 1
        if (countdownText != null)
            countdownText.text = "1";

        yield return new WaitForSecondsRealtime(1f);

        // GO!
        if (countdownText != null)
            countdownText.text = "เริ่ม!";

        yield return new WaitForSecondsRealtime(0.5f);

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        // Start actual game
        gameStarted = true;
        isCountingDown = false;

        Time.timeScale = 1f;

        Debug.Log("GAME STARTED!");
    }

    // =========================================
    // ADD SCORE
    // =========================================
    public void AddScore()
    {
        if (!gameStarted || isGameOver)
            return;

        obstaclesPassed++;
        currentScore += scorePerObstacle;

        UpdateUI();
    }

    // =========================================
    // UPDATE TIME
    // =========================================
    void UpdateTimeUI()
    {
        if (timeText == null)
            return;

        int minutes = Mathf.FloorToInt(gameTime / 60f);
        int seconds = Mathf.FloorToInt(gameTime % 60f);

        timeText.text =
            string.Format("เวลา {0:00}:{1:00}", minutes, seconds);
    }

    // =========================================
    // UPDATE SCORE
    // =========================================
    void UpdateUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "คะแนน " + currentScore;
        }

        UpdateTimeUI();
    }

    // =========================================
    // GAME OVER
    // =========================================
    public void GameOver()
    {
        if (isGameOver)
            return;

        isGameOver = true;
        gameStarted = false;

        int minutes = Mathf.FloorToInt(gameTime / 60f);
        int seconds = Mathf.FloorToInt(gameTime % 60f);

        if (summaryTimeText != null)
        {
            summaryTimeText.text =
                string.Format(
                    "เวลา {0:00}:{1:00}",
                    minutes,
                    seconds
                );
        }

        if (summaryScoreText != null)
        {
            summaryScoreText.text =
                "คะแนนรวม " + currentScore;
        }

        if (summaryObstacleText != null)
        {
            summaryObstacleText.text =
                "หลบสิ่งกีดขวางได้ " + obstaclesPassed + " ชิ้น";
        }

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        Time.timeScale = 0f;

        // บันทึกผลขึ้น Supabase (ส่งได้แม้ timeScale = 0)
        GameDataRecorder.RecordBreatheFocus(
            currentScore,
            gameTime,
            obstaclesPassed
        );
    }
}