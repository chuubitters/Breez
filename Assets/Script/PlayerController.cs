using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Flight Settings")]
    public float flySpeed = 6f;
    public float gravityScale = 2f;

    [Header("Breath Settings")]
    [Range(0, 100)]
    public int breathThreshold = 50;

    [Header("Arduino")]
    public BreathReceiver breathReceiver;

    [Header("UI Settings")]
    public GameObject gameOverUI;

    private Rigidbody2D rb;
    private Animator anim;

    private bool isFlying = false;
    private bool wasFlying = false;
    private bool isGameOver = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        rb.gravityScale = gravityScale;

        if (gameOverUI != null)
        {
            gameOverUI.SetActive(false);
        }
    }

    void Update()
    {
        if (isGameOver)
            return;

        // =========================
        // Keyboard Input
        // =========================
        bool arrowInput = Input.GetKey(KeyCode.UpArrow);

        // =========================
        // Breath Input
        // =========================
        bool breathInput = false;

        if (breathReceiver != null)
        {
            breathInput =
                breathReceiver.breathLevel >= breathThreshold;
        }

        // =========================
        // รวม Input ทั้งสองแบบ
        // =========================
        isFlying = arrowInput || breathInput;

        // =========================
        // เล่น Animation ตอนเริ่มบิน
        // =========================
        if (isFlying && !wasFlying)
        {
            if (anim != null)
            {
                anim.Play("PlayerAnim", -1, 0f);
            }
        }

        wasFlying = isFlying;
    }

    void FixedUpdate()
    {
        if (isGameOver)
            return;

        if (isFlying)
        {
            rb.velocity = new Vector2(
                rb.velocity.x,
                flySpeed
            );
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            TriggerGameOver();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            TriggerGameOver();
        }
    }

    void TriggerGameOver()
    {
        isGameOver = true;

        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
        }

        Time.timeScale = 0f;
    }
}