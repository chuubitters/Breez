using UnityEngine;
using TMPro;

public class PWGameManager : MonoBehaviour
{
    public static PWGameManager instance;

    [Header("UI Parent")]
    public Transform canvasTransform;

    [Header("Gameplay UI")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI progressText;

    [Header("Game Over UI Popup")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI timeTakenText;
    public TextMeshProUGUI resultText;

    [Header("Game Settings")]
    public GameObject redBalloonPrefab;
    public GameObject blueBalloonPrefab;
    public int totalBalloons = 20;
    public float timeLimit = 50f;

    [Header("Breath Settings")]
    [Range(0, 100)]
    public int breathThreshold = 50;

    [Header("Arduino")]
    public BreathReceiver breathReceiver;

    private int poppedCount = 0;
    private int currentScore = 0;
    private float timeElapsed = 0f;
    private bool isGameOver = false;

    private GameObject currentBalloon;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    void Start()
    {
        gameOverPanel.SetActive(false);

        SpawnBalloon();
        UpdateUI();
    }

    void Update()
    {
        if (isGameOver)
            return;

        timeElapsed += Time.deltaTime;

        UpdateUI();

        CheckBreathInput();

        if (timeElapsed >= timeLimit &&
            poppedCount < totalBalloons)
        {
            EndGame(false);
        }
    }

    void CheckBreathInput()
    {
        if (breathReceiver == null)
            return;

        if (currentBalloon == null)
            return;

        int breathLevel = breathReceiver.breathLevel;

        // ถ้าค่าเป่าถึง threshold
        if (breathLevel >= breathThreshold)
        {
            Balloon balloon = currentBalloon.GetComponent<Balloon>();

            if (balloon != null)
            {
                // ทำ Animation พองลูกโป่ง
                balloon.PumpFromBreath();
            }
        }
    }

    public void SpawnBalloon()
    {
        if (poppedCount >= totalBalloons)
        {
            EndGame(true);
            return;
        }

        int pointValue = Random.Range(1, 3);

        GameObject prefabToSpawn;

        if (pointValue == 1)
        {
            prefabToSpawn = redBalloonPrefab;
        }
        else
        {
            prefabToSpawn = blueBalloonPrefab;
        }

        currentBalloon = Instantiate(
            prefabToSpawn,
            canvasTransform
        );

        RectTransform rect =
            currentBalloon.GetComponent<RectTransform>();

        if (rect != null)
        {
            rect.anchoredPosition = Vector2.zero;
        }

        Balloon balloonScript =
            currentBalloon.GetComponent<Balloon>();

        if (balloonScript != null)
        {
            balloonScript.pointValue = pointValue;
        }
    }

    public void OnBalloonPopped(int points)
    {
        currentScore += points;
        poppedCount++;

        if (currentBalloon != null)
        {
            Destroy(currentBalloon);
            currentBalloon = null;
        }

        SpawnBalloon();
    }

    void UpdateUI()
    {
        float timeLeft =
            Mathf.Max(timeLimit - timeElapsed, 0);

        timerText.text =
            timeLeft.ToString("F1") + " s";

        progressText.text =
            "Popped: " +
            poppedCount +
            " / " +
            totalBalloons;
    }

    void EndGame(bool isWin)
    {
        isGameOver = true;

        if (currentBalloon != null)
        {
            Destroy(currentBalloon);
            currentBalloon = null;
        }

        gameOverPanel.SetActive(true);

        resultText.text =
            isWin ? "MISSION COMPLETE!" : "TIME'S UP!";

        finalScoreText.text =
            "Total Score: " + currentScore;

        timeTakenText.text =
            "Time Taken: " +
            timeElapsed.ToString("F2") +
            " Seconds";

        // บันทึกผลขึ้น Supabase (หลังบ้านเห็นในหน้า ประวัติการเล่น)
        GameDataRecorder.RecordPowerRelease(
            currentScore,
            timeElapsed,
            totalBalloons,
            poppedCount
        );
    }
}