using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class Balloon : MonoBehaviour
{
    public int pointValue = 1;
    public float inflateSpeed = 0.5f;
    public float maxScaleMultiplier = 1.5f;

    [Header("Balloon Sprites (5 Steps)")]
    public Sprite[] balloonSprites;

    private Image balloonImage;
    private float currentProgress = 0f;
    private Vector3 initialScale;
    private Vector3 maxScale;

    void Start()
    {
        balloonImage = GetComponent<Image>();

        initialScale = transform.localScale;
        maxScale = initialScale * maxScaleMultiplier;

        if (balloonSprites != null && balloonSprites.Length > 0)
        {
            balloonImage.sprite = balloonSprites[0];
        }
    }

    void Update()
    {
        // เก็บการกดลูกศรไว้เหมือนเดิม
        if (Input.GetKey(KeyCode.UpArrow))
        {
            PumpBalloon();
        }
    }

    // ให้ PWGameManager เรียกฟังก์ชันนี้ตอนมีการเป่า
    public void PumpFromBreath()
    {
        PumpBalloon();
    }

    public void PumpBalloon()
    {
        // ป้องกันไม่ให้ Progress เกิน 1
        if (currentProgress >= 1f)
            return;

        currentProgress += inflateSpeed * Time.deltaTime;

        // เสียงลูกโป่งพอง (เว้นช่วงไม่ให้ถี่เกิน เสียงสูงขึ้นตามขนาด)
        if (Time.time >= nextInflateSound)
        {
            nextInflateSound = Time.time + 0.2f;
            BreezAudio.Play("balloon_inflate", 0.8f, 0.85f + currentProgress * 0.7f);
        }

        currentProgress = Mathf.Clamp01(currentProgress);

        // ขยายลูกโป่ง
        transform.localScale = Vector3.Lerp(
            initialScale,
            maxScale,
            currentProgress
        );

        // เปลี่ยน Sprite ตาม Step
        if (balloonSprites != null && balloonSprites.Length > 0)
        {
            int stepIndex = Mathf.FloorToInt(
                currentProgress * balloonSprites.Length
            );

            stepIndex = Mathf.Clamp(
                stepIndex,
                0,
                balloonSprites.Length - 1
            );

            balloonImage.sprite = balloonSprites[stepIndex];
        }

        // แตกเมื่อเต็ม
        if (currentProgress >= 1f)
        {
            Pop();
        }
    }

    static float nextInflateSound;

    void Pop()
    {
        BreezAudio.Play("balloon_pop");
        PWGameManager.instance.OnBalloonPopped(pointValue);
    }
}