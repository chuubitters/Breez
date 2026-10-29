using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// Calm Flow: ดอกไม้ (UI) ที่ค่อยๆ บานตาม Progress 0-1
//   0    = ต้นกล้า: ก้านสั้น ดอกตูมสีเขียวอ่อน
//   0.5  = ใบกาง ดอกตูมใหญ่ขึ้น กลีบเริ่มแย้ม
//   1    = ก้านสูง กลีบชั้นนอก + ชั้นในบานเต็มที่ เกสรกลางดอก มีแสงฟุ้ง
// CalmFlowGame เป็นคนตั้ง Progress ส่วนนี้ทำแค่ภาพ (ค่อยๆ ไล่ตามค่า ให้ดูนุ่มนวล)
// โครงสร้างสร้างด้วยเมนู Breez > Create Calm Flow Scene
// =====================================================================
public class BloomFlower : MonoBehaviour
{
    [Range(0f, 1f)] public float progress;
    [Tooltip("ความเร็วที่ภาพไล่ตาม progress (ยิ่งมากยิ่งไว)")]
    public float follow = 3.5f;
    [Tooltip("เป่าอยู่ = ดอกขยับ/หายใจตาม")]
    public bool breathing;

    [Header("Parts")]
    public RectTransform stem;
    public RectTransform head;
    public RectTransform leafLeft, leafRight;
    public RectTransform[] outerPetals;
    public RectTransform[] innerPetals;
    public RectTransform center;
    public Image glow;

    [Header("Size (px)")]
    public float stemMin = 70f, stemMax = 330f;
    public float stemWidth = 16f;
    public Vector2 outerPetalSize = new Vector2(120, 190);
    public Vector2 innerPetalSize = new Vector2(90, 140);
    public float centerSize = 78f;
    public Vector2 leafSize = new Vector2(80, 150);

    [Header("Colors")]
    public Color budColor = new Color32(0xC9, 0xDE, 0x9F, 255);
    public Color blushColor = new Color32(0xFB, 0xEA, 0xE2, 255);
    public Color outerColor = new Color32(0xF6, 0xBF, 0xCF, 255);
    public Color innerColor = new Color32(0xF2, 0xA0, 0xB8, 255);
    public Color centerColor = new Color32(0xF7, 0xD7, 0x6C, 255);

    public float Shown { get; private set; }
    float swayPhase;

    void Awake() { swayPhase = Random.value * 10f; }

    /// <summary>ตั้งภาพทันที (ไม่ไล่ค่า) เช่น ดอกที่ปลูกแล้วในสวน</summary>
    public void SetInstant(float p)
    {
        progress = Mathf.Clamp01(p);
        Shown = progress;
        Apply(Shown, 0f);
    }

    void Update()
    {
        Shown = Mathf.MoveTowards(Shown, progress, Time.deltaTime * follow * Mathf.Max(0.08f, Mathf.Abs(progress - Shown)));
        if (Mathf.Abs(Shown - progress) < 0.002f) Shown = progress;
        Apply(Shown, Time.time);
    }

    static float Smooth(float a, float b, float t) { t = Mathf.Clamp01((t - a) / (b - a)); return t * t * (3f - 2f * t); }

    void Apply(float p, float time)
    {
        float grow = Smooth(0f, 0.55f, p);       // ก้าน + ใบ
        float open = Smooth(0.25f, 1f, p);       // กลีบชั้นนอก
        float openIn = Smooth(0.45f, 1f, p);     // กลีบชั้นใน
        float full = Smooth(0.9f, 1f, p);

        float sway = Mathf.Sin(time * 0.9f + swayPhase) * (1.5f + (breathing ? 2.5f : 0f));
        float pulse = breathing ? 1f + Mathf.Sin(time * 5f) * 0.015f : 1f;

        float h = Mathf.Lerp(stemMin, stemMax, grow);
        if (stem != null)
        {
            stem.sizeDelta = new Vector2(stemWidth, h);
            stem.localRotation = Quaternion.Euler(0, 0, sway * 0.6f);
        }
        if (head != null && stem != null)
        {
            // หัวดอกอยู่ปลายก้าน (หมุนตามก้าน)
            float rad = (90f + sway * 0.6f) * Mathf.Deg2Rad;
            head.anchoredPosition = stem.anchoredPosition + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * h;
            head.localRotation = Quaternion.Euler(0, 0, sway);
            head.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, Smooth(0f, 0.8f, p)) * pulse;
        }

        // ใบ: กางออกจากก้านตั้งแต่ช่วงแรก
        float leafK = Smooth(0.05f, 0.5f, p);
        PlaceLeaf(leafLeft, h * 0.32f, 1f, leafK, sway, time);
        PlaceLeaf(leafRight, h * 0.5f, -1f, leafK * 0.9f, sway, time);

        Petals(outerPetals, outerPetalSize, open, Mathf.Lerp(0.42f, 1f, open), 0f, outerColor, p);
        Petals(innerPetals, innerPetalSize, openIn, Mathf.Lerp(0.38f, 1f, openIn) * Mathf.Lerp(0.85f, 1f, open), 180f / Mathf.Max(1, innerPetals != null ? innerPetals.Length : 1), innerColor, p);

        if (center != null)
        {
            float c = Smooth(0.35f, 0.95f, p);
            center.sizeDelta = Vector2.one * centerSize;
            center.localScale = Vector3.one * Mathf.Lerp(0.0001f, 1f, c);
            var img = center.GetComponent<Image>();
            if (img != null) img.color = centerColor;
        }
        if (glow != null)
        {
            var g = glow.color;
            g.a = full * (0.55f + 0.2f * Mathf.Sin(time * 2.2f));
            glow.color = g;
            glow.rectTransform.localScale = Vector3.one * (0.9f + 0.1f * Mathf.Sin(time * 1.3f));
        }
    }

    void PlaceLeaf(RectTransform leaf, float y, float side, float k, float sway, float time)
    {
        if (leaf == null || stem == null) return;
        float rad = (90f + sway * 0.6f) * Mathf.Deg2Rad;
        leaf.anchoredPosition = stem.anchoredPosition + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * y;
        leaf.sizeDelta = leafSize;
        float flutter = Mathf.Sin(time * 1.7f + side) * 3f;
        leaf.localRotation = Quaternion.Euler(0, 0, side * Mathf.Lerp(8f, 58f, k) + sway + flutter);
        leaf.localScale = Vector3.one * Mathf.Max(0.0001f, k);
    }

    void Petals(RectTransform[] petals, Vector2 size, float open, float scale, float offsetDeg, Color bloomColor, float p)
    {
        if (petals == null || petals.Length == 0) return;
        int n = petals.Length;
        // เขียวอ่อน -> ครีมอมชมพู -> สีดอก (ไม่ผ่านสีหม่นๆ ตรงกลาง)
        float t = Smooth(0.15f, 0.75f, p);
        var col = t < 0.5f ? Color.Lerp(budColor, blushColor, t * 2f) : Color.Lerp(blushColor, bloomColor, t * 2f - 1f);
        for (int i = 0; i < n; i++)
        {
            var r = petals[i];
            if (r == null) continue;
            // ตูม: กลีบเกาะกันชี้ขึ้น (กระจาย ±18°)  บาน: กระจายรอบวง
            float closed = Mathf.Lerp(-18f, 18f, n == 1 ? 0.5f : i / (float)(n - 1));
            float target = offsetDeg + i * 360f / n;
            // ให้กลีบหมุนไปทางที่ใกล้ที่สุด
            float delta = Mathf.DeltaAngle(closed, target);
            r.localRotation = Quaternion.Euler(0, 0, closed + delta * open);
            r.sizeDelta = size;
            r.localScale = new Vector3(Mathf.Lerp(0.55f, 1f, open), 1f, 1f) * scale;
            var img = r.GetComponent<Image>();
            if (img != null) img.color = col;
        }
    }
}
