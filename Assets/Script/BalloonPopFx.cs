using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// เอฟเฟกต์ลูกโป่งแตก (UI): แฟลชรูปลูกโป่ง + วงคลื่น + เศษ/คอนเฟตตีกระจาย + "+คะแนน" ลอยขึ้น
// สร้างจาก Balloon.Pop()  ลบตัวเองเมื่อจบ (~1 วินาที)
// =====================================================================
public class BalloonPopFx : MonoBehaviour
{
    class Bit { public RectTransform rt; public Image img; public Vector2 vel; public float spin, life, size; }

    static Sprite circle, ring, shard;
    readonly List<Bit> bits = new List<Bit>();
    RectTransform flash, wave;
    Image flashImg, waveImg;
    TMP_Text plus;
    Vector2 plusStart;
    Color color;
    float t;
    const float Duration = 1.1f;

    public static void Spawn(RectTransform balloon, Image balloonImage, Color color, int points)
    {
        var parent = balloon.parent as RectTransform;
        if (parent == null) return;
        var go = new GameObject("BalloonPopFx", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = balloon.anchorMin;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        // จุดกลางลูกโป่ง (ตามขนาดจริงตอนแตก)
        Vector3 center = balloon.TransformPoint(balloon.rect.center);
        rt.position = center;
        rt.SetSiblingIndex(balloon.GetSiblingIndex() + 1);
        var fx = go.AddComponent<BalloonPopFx>();
        float radius = Mathf.Max(60f, balloon.rect.width * Mathf.Abs(balloon.localScale.x) * 0.45f);
        fx.Build(balloonImage, balloon, color, points, radius);
    }

    void Build(Image balloonImage, RectTransform balloon, Color c, int points, float radius)
    {
        color = c;
        EnsureSprites();

        // แฟลชรูปลูกโป่งเดิม: ขยายเร็วแล้วหาย
        if (balloonImage != null && balloonImage.sprite != null)
        {
            flashImg = NewImage("Flash", balloonImage.sprite, Color.white);
            flash = flashImg.rectTransform;
            flash.sizeDelta = balloon.rect.size;
            flash.localScale = balloon.localScale;
            flash.localRotation = balloon.localRotation;
            flash.position = balloon.TransformPoint(balloon.rect.center);
            flashImg.preserveAspect = balloonImage.preserveAspect;
        }

        // วงคลื่น
        waveImg = NewImage("Wave", ring, new Color(1f, 1f, 1f, 0.9f));
        wave = waveImg.rectTransform;
        wave.sizeDelta = Vector2.one * radius * 1.2f;

        // เศษลูกโป่ง (สีลูกโป่ง) + คอนเฟตตีพาสเทล
        Color[] confetti = { c, c, new Color32(0xF6, 0xE3, 0xA0, 255), new Color32(0xD4, 0xE5, 0x9D, 255), new Color32(0xF5, 0xC6, 0xD2, 255), Color.white };
        int count = 22;
        for (int i = 0; i < count; i++)
        {
            bool isShard = i < 9;
            var img = NewImage("Bit", isShard ? shard : circle, confetti[isShard ? 0 : Random.Range(1, confetti.Length)]);
            float ang = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
            float speed = Random.Range(380f, 760f) * (isShard ? 1.1f : 0.9f);
            var b = new Bit
            {
                rt = img.rectTransform, img = img,
                vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed,
                spin = Random.Range(-540f, 540f),
                life = Random.Range(0.7f, 1.05f),
                size = isShard ? Random.Range(26f, 44f) : Random.Range(10f, 18f),
            };
            b.rt.sizeDelta = isShard ? new Vector2(b.size, b.size * 0.6f) : Vector2.one * b.size;
            b.rt.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius * 0.35f;
            b.rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            bits.Add(b);
        }

        // +คะแนน
        var tgo = new GameObject("Plus", typeof(RectTransform));
        tgo.transform.SetParent(transform, false);
        plus = tgo.AddComponent<TextMeshProUGUI>();
        var anyText = FindObjectOfType<TMP_Text>();
        if (anyText != null) plus.font = anyText.font;
        plus.text = "+" + points;
        plus.fontSize = 72;
        plus.fontStyle = FontStyles.Bold;
        plus.alignment = TextAlignmentOptions.Center;
        plus.color = Color.white;
        plus.outlineWidth = 0.25f;
        plus.outlineColor = new Color32(0x5E, 0x7D, 0x45, 255);
        plus.raycastTarget = false;
        plus.rectTransform.sizeDelta = new Vector2(240, 100);
        plusStart = new Vector2(0, radius * 0.3f);
        plus.rectTransform.anchoredPosition = plusStart;
    }

    Image NewImage(string n, Sprite s, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var img = go.AddComponent<Image>();
        img.sprite = s;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        t += dt;
        float k = t / Duration;

        if (flash != null)
        {
            float f = Mathf.Clamp01(t / 0.14f);
            flash.localScale *= 1f + dt * 6f;
            flashImg.color = new Color(1f, 1f, 1f, 1f - f);
            if (f >= 1f) { Destroy(flash.gameObject); flash = null; }
        }
        if (wave != null)
        {
            float w = Mathf.Clamp01(t / 0.45f);
            wave.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.4f, 1f - Mathf.Pow(1f - w, 3f));
            waveImg.color = new Color(1f, 1f, 1f, 0.85f * (1f - w));
        }
        foreach (var b in bits)
        {
            if (b.rt == null) continue;
            float life = Mathf.Clamp01(t / b.life);
            b.vel *= Mathf.Exp(-3.2f * dt);         // แรงต้านอากาศ
            b.vel.y -= 900f * dt;                   // แรงโน้มถ่วง
            b.rt.anchoredPosition += b.vel * dt;
            b.rt.localRotation *= Quaternion.Euler(0, 0, b.spin * dt);
            float flutter = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 12f + b.spin));
            b.rt.localScale = new Vector3(1f, flutter, 1f) * (1f - life * 0.4f);
            var c = b.img.color; c.a = 1f - Mathf.Pow(life, 2f); b.img.color = c;
        }
        if (plus != null)
        {
            float p = Mathf.Clamp01(t / 0.9f);
            float pop = p < 0.2f ? Mathf.Lerp(0.4f, 1.25f, p / 0.2f) : Mathf.Lerp(1.25f, 1f, (p - 0.2f) / 0.3f);
            plus.rectTransform.localScale = Vector3.one * pop;
            plus.rectTransform.anchoredPosition = plusStart + new Vector2(0, 140f * (1f - Mathf.Pow(1f - p, 2f)));
            var c = plus.color; c.a = p < 0.65f ? 1f : 1f - (p - 0.65f) / 0.35f; plus.color = c;
        }
        if (k >= 1f) Destroy(gameObject);
    }

    // ---------- สไปรต์ขอบเรียบ (สร้างครั้งเดียว) ----------
    static void EnsureSprites()
    {
        if (circle != null) return;
        circle = MakeSprite(64, (x, y) => Mathf.Clamp01(32f - Mathf.Sqrt(x * x + y * y)));
        ring = MakeSprite(128, (x, y) => { float d = Mathf.Sqrt(x * x + y * y); return Mathf.Clamp01(Mathf.Min(d - 50f, 62f - d)); });
        // เศษลูกโป่ง: วงรีหยักเล็กน้อย
        shard = MakeSprite(64, (x, y) =>
        {
            float a = Mathf.Atan2(y, x);
            float r = 28f + 4f * Mathf.Sin(a * 3f);
            return Mathf.Clamp01(r - Mathf.Sqrt(x * x + y * y));
        });
    }

    static Sprite MakeSprite(int n, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        float h = n / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = new Color32(255, 255, 255, (byte)(alpha(x + 0.5f - h, y + 0.5f - h) * 255f));
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
