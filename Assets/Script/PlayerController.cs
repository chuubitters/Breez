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
    public float fallSpeed = 6f;
    [Tooltip("ความไวในการเปลี่ยนทิศ (ยิ่งมากยิ่งทันที)")]
    public float responsiveness = 60f;

    [Header("Breath Settings")]
    [Range(0, 100)]
    [Tooltip("แรงเป่าที่เริ่มนับว่าเป่า (ยิ่งต่ำยิ่งไว)")]
    public int breathThreshold = 30;
    [Range(0, 100)]
    [Tooltip("ต่ำกว่าค่านี้ถึงนับว่าหยุดเป่า (กันผึ้งกระตุกตอนแรงเป่าอยู่ใกล้ขีด) -1 = ใช้ breathThreshold")]
    public int releaseThreshold = -1;

    [Header("Arduino")]
    public BreathReceiver breathReceiver;

    [Header("UI Settings")]
    public GameObject gameOverUI;

    [Header("ขอบจอบน")]
    [Tooltip("เป่านานแค่ไหนก็ไม่ลอยพ้นขอบจอบน (ไม่เป่า = ตกลงได้ตามปกติ)")]
    public bool clampToTop = true;
    [Tooltip("เว้นระยะจากขอบจอบน (หน่วย world)")]
    public float topMargin = 0.15f;
    [Tooltip("ไม่ให้ตกพ้นขอบจอล่าง (โหมดเก็บดอกไม้: ไม่มีการตาย)")]
    public bool clampToBottom = false;
    public float bottomMargin = 0.15f;

    private Rigidbody2D rb;
    private Animator anim;

    private bool isFlying = false;
    private bool breathing = false;
    private float nextWhoosh;
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
            breathInput = DetectBreath(breathReceiver.breathLevel);

        // =========================
        // รวม Input ทั้งสองแบบ
        // =========================
        isFlying = arrowInput || breathInput;

        // =========================
        // เล่น Animation ตอนเริ่มบิน
        // =========================
        if (isFlying && !wasFlying)
        {
            if (Time.unscaledTime >= nextWhoosh)
            {
                nextWhoosh = Time.unscaledTime + 0.3f;
                if (Time.timeScale > 0f) BreezAudio.Play("focus_whoosh", 0.8f);
            }
            if (anim != null)
            {
                anim.Play("PlayerAnim", -1, 0f);
            }
        }

        wasFlying = isFlying;
    }

    // ---------------------------------------------------------------
    // ตรวจจับการเป่า: เป่าปุ๊บ = ลอย, หยุดเป่าปุ๊บ = ตก
    //   • เริ่มเป่า: แรงเป่าเกิน breathThreshold และสูงกว่าเสียงรบกวนรอบตัว
    //   • หยุดเป่า: แรงเป่าลดลงต่ำกว่าครึ่งหนึ่งของแรงที่เพิ่งเป่า หรือต่ำกว่า releaseThreshold
    //     (ไม่ต้องรอให้ค่ากลับไปเป็น 0 เพราะไมค์มักมีเสียงค้าง ทำให้ผึ้งไม่ตก)
    // ---------------------------------------------------------------
    float noiseFloor, peak, recentMax, nextLog;

    [Header("ตรวจจับการเป่า")]
    [Tooltip("แสดงค่าแรงเป่าและสถานะใน Console (ใช้ปรับค่า)")]
    public bool logBreath = false;
    [Tooltip("แรงเป่าที่ลดลงจากเมื่อครู่ (ภายใน ~0.15 วินาที) มากกว่าค่านี้ = หยุดเป่า")]
    public float dropToStop = 18f;

    bool DetectBreath(int level)
    {
        float dt = Time.unscaledDeltaTime;
        recentMax = Mathf.Max(level, recentMax - 120f * dt);   // ค่าสูงสุดช่วงสั้นๆ ล่าสุด
        if (!breathing)
        {
            // เสียงรบกวนพื้นหลัง: ลงเร็ว ขึ้นช้า
            noiseFloor = level < noiseFloor ? Mathf.Lerp(noiseFloor, level, 10f * dt) : Mathf.MoveTowards(noiseFloor, level, 4f * dt);
            float start = Mathf.Max(breathThreshold, noiseFloor + 15f);
            if (level >= start) { breathing = true; peak = level; recentMax = level; }
        }
        else
        {
            peak = Mathf.Max(level, peak - 40f * dt);       // แรงเป่าล่าสุด
            float release = Mathf.Max(releaseThreshold >= 0 ? releaseThreshold : breathThreshold * 0.6f, noiseFloor + 8f, peak * 0.6f);
            bool dropped = recentMax - level >= dropToStop;  // แรงเป่าตกฮวบ = หยุดเป่าแล้ว (ไม่ต้องรอค่าลงจนต่ำ)
            if (level < release || dropped) { breathing = false; noiseFloor = Mathf.Min(noiseFloor, level); }
        }
        if (logBreath && Time.unscaledTime >= nextLog)
        {
            nextLog = Time.unscaledTime + 0.2f;
            Debug.Log($"[Breez][Breath] level={level} peak={peak:0} floor={noiseFloor:0} blowing={breathing}");
        }
        return breathing;
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
        ClampBottom();
    }

    void ClampBottom()
    {
        if (!clampToBottom) return;
        var cam = Camera.main;
        float bottom = cam != null && cam.orthographic ? cam.transform.position.y - cam.orthographicSize : -5f;
        float half = 0.5f;
        var col = GetComponent<Collider2D>();
        if (col != null) half = col.bounds.extents.y;
        float centerOffset = col != null ? col.bounds.center.y - rb.position.y : 0f;
        float limit = bottom + half - centerOffset + bottomMargin;
        if (rb.position.y > limit) return;
        rb.position = new Vector2(rb.position.x, limit);
        if (rb.velocity.y < 0f) rb.velocity = new Vector2(rb.velocity.x, 0f);
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