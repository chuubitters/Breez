using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// ลูกโป่ง Power Release
//   • เกิด: เด้งขึ้นมาจากด้านล่าง (ease-out-back)
//   • ลอยเบาๆ + โยกซ้ายขวา
//   • เป่า: พองขึ้นนุ่มๆ + เด้งแบบเยลลี่ทุกครั้งที่เป่า
//   • ใกล้แตก: สั่น + แดงเรื่อๆ (ตึงมาก)
//   • แตก: แฟลช + วงคลื่น + เศษลูกโป่ง/คอนเฟตตีกระจาย + "+คะแนน" ลอยขึ้น (BalloonPopFx)
// =====================================================================
[RequireComponent(typeof(Image))]
public class Balloon : MonoBehaviour
{
    public int pointValue = 1;
    public float inflateSpeed = 0.5f;
    public float maxScaleMultiplier = 1.5f;

    [Header("Balloon Sprites (5 Steps)")]
    public Sprite[] balloonSprites;

    [Header("แอนิเมชัน")]
    public bool animate = true;
    [Tooltip("ระยะลอยขึ้นลง (px)")] public float bobAmount = 10f;
    [Tooltip("สีเศษตอนแตก (ไม่ตั้ง = เดาจากชื่อ prefab: แดง/ฟ้า)")] public Color burstColor = new Color(0, 0, 0, 0);

    Image balloonImage;
    RectTransform rt;
    float currentProgress, shown, spawnT, wobble, lastPump = -9f, seed;
    Vector3 initialScale;
    Vector2 basePos;
    bool posCaptured, popped;

    static float nextInflateSound;

    void Start()
    {
        balloonImage = GetComponent<Image>();
        rt = transform as RectTransform;
        initialScale = transform.localScale;
        seed = Random.value * 10f;
        if (balloonSprites != null && balloonSprites.Length > 0) balloonImage.sprite = balloonSprites[0];
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.UpArrow)) PumpBalloon();
    }

    /// <summary>PWGameManager เรียกตอนมีการเป่า</summary>
    public void PumpFromBreath() => PumpBalloon();

    public void PumpBalloon()
    {
        if (popped || currentProgress >= 1f) return;

        currentProgress = Mathf.Clamp01(currentProgress + inflateSpeed * Time.deltaTime);
        lastPump = Time.time;
        wobble = Mathf.Min(1f, wobble + Time.deltaTime * 5f);

        if (Time.time >= nextInflateSound)
        {
            nextInflateSound = Time.time + 0.2f;
            BreezAudio.Play("balloon_inflate", 0.8f, 0.85f + currentProgress * 0.7f);
        }

        if (balloonSprites != null && balloonSprites.Length > 0)
        {
            int step = Mathf.Clamp(Mathf.FloorToInt(currentProgress * balloonSprites.Length), 0, balloonSprites.Length - 1);
            balloonImage.sprite = balloonSprites[step];
        }

        if (!animate) transform.localScale = Vector3.Lerp(initialScale, initialScale * maxScaleMultiplier, currentProgress);
        if (currentProgress >= 1f) Pop();
    }

    void LateUpdate()
    {
        if (!animate || rt == null || popped) return;
        if (!posCaptured) { basePos = rt.anchoredPosition; posCaptured = true; }   // ตำแหน่งที่ GameManager วางไว้

        float dt = Time.deltaTime, t = Time.time;
        spawnT = Mathf.Min(1f, spawnT + dt * 2.4f);
        float spawn = EaseOutBack(spawnT);
        shown = Mathf.Lerp(shown, currentProgress, 1f - Mathf.Exp(-9f * dt));
        wobble = Mathf.MoveTowards(wobble, 0f, dt * 1.2f);
        bool pumping = t - lastPump < 0.15f;

        // เยลลี่: กว้าง/สูง สลับกัน
        float j = Mathf.Sin(t * 17f) * 0.05f * wobble;
        // ตึงมาก: สั่น
        float tense = Mathf.Clamp01((shown - 0.78f) / 0.22f);
        float shakeX = (Mathf.PerlinNoise(t * 22f, seed) - 0.5f) * 10f * tense;
        float shakeY = (Mathf.PerlinNoise(seed, t * 22f) - 0.5f) * 6f * tense;
        // ลอย + โยก
        float bob = Mathf.Sin(t * 1.7f + seed) * bobAmount * (pumping ? 0.4f : 1f);
        float sway = Mathf.Sin(t * 1.1f + seed) * 4f + shakeX * 0.3f;

        float size = Mathf.Lerp(1f, maxScaleMultiplier, shown) * Mathf.Max(0.001f, spawn);
        transform.localScale = new Vector3(initialScale.x * size * (1f + j), initialScale.y * size * (1f - j), initialScale.z);
        rt.anchoredPosition = basePos + new Vector2(shakeX, bob + shakeY - (1f - EaseOut(spawnT)) * 160f);
        rt.localRotation = Quaternion.Euler(0f, 0f, sway);

        if (balloonImage != null)
        {
            float flush = tense * 0.35f * (0.6f + 0.4f * Mathf.Sin(t * 24f));
            balloonImage.color = Color.Lerp(Color.white, new Color(1f, 0.78f, 0.78f), flush);
        }
    }

    void Pop()
    {
        if (popped) return;
        popped = true;
        BreezAudio.Play("balloon_pop");
        if (animate && rt != null) BalloonPopFx.Spawn(rt, balloonImage, BurstColor(), pointValue);
        PWGameManager.instance.OnBalloonPopped(pointValue);
    }

    Color BurstColor()
    {
        if (burstColor.a > 0f) return burstColor;
        string n = name.ToLowerInvariant();
        if (n.Contains("blue")) return new Color32(0x8E, 0xC5, 0xF0, 255);
        if (n.Contains("red")) return new Color32(0xF2, 0x8B, 0x9B, 255);
        return pointValue == 1 ? new Color32(0xF2, 0x8B, 0x9B, 255) : new Color32(0x8E, 0xC5, 0xF0, 255);
    }

    static float EaseOut(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);
    static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
