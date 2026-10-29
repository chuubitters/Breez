using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Flight Settings")]
    public float flySpeed = 6f;
    public float gravityScale = 2f;

    [Header("ตอบสนองทันที (ไม่ดีเลย์)")]
    [Tooltip("เป่า = ลอยขึ้นทันที, หยุดเป่า = ตกลงทันที (ไม่ใช้แรงโน้มถ่วงที่ค่อยๆ ดึงลง)")]
    public bool instantControl = true;
    [Tooltip("ความเร็วลอยขึ้น (หน่วย/วินาที) ตอนเป่า")]
    public float riseSpeed = 5f;
    [Tooltip("ความเร็วตก (หน่วย/วินาที) ตอนไม่เป่า")]
    public float fallSpeed = 4.5f;
    [Tooltip("ความไวในการเปลี่ยนทิศ (ยิ่งมากยิ่งทันที)")]
    public float responsiveness = 60f;

    [Header("Breath Settings")]
    [Range(0, 100)]
    public int breathThreshold = 50;

    [Header("Arduino")]
    public BreathReceiver breathReceiver;

    [Header("UI Settings")]
    public GameObject gameOverUI;

    [Header("ขอบจอบน")]
    [Tooltip("เป่านานแค่ไหนก็ไม่ลอยพ้นขอบจอบน (ไม่เป่า = ตกลงได้ตามปกติ)")]
    public bool clampToTop = true;
    [Tooltip("เว้นระยะจากขอบจอบน (หน่วย world)")]
    public float topMargin = 0.15f;

    private Rigidbody2D rb;
    private Animator anim;

    private bool isFlying = false;
    private bool wasFlying = false;
    private bool isGameOver = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        rb.gravityScale = instantControl ? 0f : gravityScale;

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

        if (instantControl)
        {
            // เป่า = ขึ้น, ไม่เป่า = ลง เปลี่ยนทิศแทบทันที (ไม่มีแรงเฉื่อยค้าง)
            float target = isFlying ? riseSpeed : -fallSpeed;
            float vy = Mathf.MoveTowards(rb.velocity.y, target, responsiveness * Time.fixedDeltaTime);
            rb.velocity = new Vector2(rb.velocity.x, vy);
        }
        else if (isFlying)
        {
            rb.velocity = new Vector2(
                rb.velocity.x,
                flySpeed
            );
        }

        ClampTop();
    }

    // กันไม่ให้ลอยพ้นขอบจอบน: หยุดที่ขอบ (ความเร็วขาขึ้น = 0) แต่ตกลงได้ปกติ
    void ClampTop()
    {
        if (!clampToTop) return;
        float limit = TopLimit();
        if (rb.position.y < limit) return;
        rb.position = new Vector2(rb.position.x, limit);
        if (rb.velocity.y > 0f) rb.velocity = new Vector2(rb.velocity.x, 0f);
    }

    float TopLimit()
    {
        var cam = Camera.main;
        float top = cam != null && cam.orthographic ? cam.transform.position.y + cam.orthographicSize : 5f;
        float half = 0.5f;
        var col = GetComponent<Collider2D>();
        if (col != null) half = col.bounds.extents.y;
        else
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) half = sr.bounds.extents.y;
        }
        // offset ระหว่างจุดหมุนกับกึ่งกลางตัวละคร
        float centerOffset = col != null ? col.bounds.center.y - rb.position.y : 0f;
        return top - half - centerOffset - topMargin;
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