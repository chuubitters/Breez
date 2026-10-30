using UnityEngine;
using TMPro;

public class PWGameManager : MonoBehaviour
{
    public static PWGameManager instance;

    [Header("UI Parent")]
    public Transform canvasTransform;
    [Tooltip("เลเยอร์ที่ลูกโป่งเกิด (อยู่เหนือ bg/เมฆ แต่ใต้ข้อความ) ถ้าว่างจะสร้างให้อัตโนมัติ")]
    public Transform balloonLayer;

    [Header("Gameplay UI")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI progressText;

    [Header("Game Over UI Popup")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI timeTakenText;
    public TextMeshProUGUI resultText;
    [Tooltip("ลูกโป่งที่เป่าได้ เช่น 20/20 (ไม่ใส่ก็ได้)")]
    public TextMeshProUGUI balloonsResultText;

    [Header("วิธีการเล่น (ไม่ใส่ = เริ่มเกมทันที)")]
    public GameObject howToPanel;
    public UnityEngine.UI.Button howToStartButton;

    [Header("Text Format")]
    [Tooltip("{0} = เป่าแตกแล้ว, {1} = ทั้งหมด")]
    public string progressFormat = "จำนวนลูกโป่ง : {0}/{1}";
    public string winText = "สำเร็จ!";
    public string loseText = "หมดเวลา!";

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

    private bool started = false;

    void Start()
    {
        gameOverPanel.SetActive(false);
        EnsureBalloonLayer();
        UpdateUI();

        if (howToPanel != null)
        {
            // แสดงวิธีการเล่นก่อน กด "เริ่ม" แล้วค่อยเริ่มจับเวลา
            howToPanel.SetActive(true);
            if (howToStartButton != null) howToStartButton.onClick.AddListener(BeginGame);
        }
        else BeginGame();
    }

    public void BeginGame()
    {
        if (started) return;
        started = true;
        if (howToPanel != null) BreezAudio.Play("count_go", 0.7f);
        if (howToPanel != null) howToPanel.SetActive(false);
        SpawnBalloon();
        UpdateUI();
    }

    void Update()
    {
        if (isGameOver || !started)
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
            balloonLayer != null ? balloonLayer : canvasTransform
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

    // ลูกโป่งต้องอยู่เหนือ bg/เมฆ แต่อยู่ใต้ Time / Progress / ป้ายสถานะ
    // ถ้าไม่ได้ตั้ง balloonLayer จะสร้าง BalloonLayer ต่อจาก bg + เมฆ ให้เอง
    void EnsureBalloonLayer()
    {
        if (balloonLayer != null || canvasTransform == null) return;

        var existing = canvasTransform.Find("BalloonLayer");
        if (existing != null) { balloonLayer = existing; return; }

        var go = new GameObject("BalloonLayer", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvasTransform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // วางต่อจากภาพพื้นหลังชิ้นแรกๆ (bg, เมฆ) = ก่อน UI ตัวแรกที่ไม่ใช่ภาพพื้นหลัง
        int index = 0;
        for (int i = 0; i < canvasTransform.childCount; i++)
        {
            var c = canvasTransform.GetChild(i);
            if (c == rt) continue;
            if (c.GetComponent<UnityEngine.UI.Image>() != null && c.childCount == 0 &&
                c.GetComponent<UnityEngine.UI.Selectable>() == null)
                index = i + 1;
            else break;
        }
        rt.SetSiblingIndex(index);
        balloonLayer = rt;
    }

    public static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds - 0.0001f));
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }

    void UpdateUI()
    {
        float timeLeft =
            Mathf.Max(timeLimit - timeElapsed, 0);

        if (timerText != null)
            timerText.text = FormatTime(timeLeft);

        if (progressText != null)
            progressText.text = string.Format(progressFormat, poppedCount, totalBalloons);
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

        UpdateUI();

        if (resultText != null)
            resultText.text = isWin ? winText : loseText;
        BreezAudio.Play(isWin ? "result_win" : "result_end");

        if (finalScoreText != null)
            finalScoreText.text = currentScore.ToString();

        if (timeTakenText != null)
            timeTakenText.text = FormatTime(Mathf.Min(timeElapsed, timeLimit));

        if (balloonsResultText != null)
            balloonsResultText.text = poppedCount + "/" + totalBalloons;

        // บันทึกผลขึ้น Supabase (หลังบ้านเห็นในหน้า ประวัติการเล่น)
        GameDataRecorder.RecordPowerRelease(
            currentScore,
            timeElapsed,
            totalBalloons,
            poppedCount
        );
    }
}