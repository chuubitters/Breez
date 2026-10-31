using System.Collections;
using TMPro;
using UnityEngine;

// ดอกไม้ 1 ดอก: ลอยเข้ามา + โยก/หมุน/เต้นเบาๆ ตอนลอย, ผึ้งชน = เก็บ (เด้ง + กลีบกระจาย + ตัวเลขคะแนน)
public class FlowerPickup : MonoBehaviour
{
    FlowerSpawner owner;
    FlowerSpawner.FlowerType type;
    Transform visual, petals, glow;
    SpriteRenderer petalsSr, centerSr, glowSr;
    float phase, baseY, born;
    bool collected;

    public void Init(FlowerSpawner spawner, FlowerSpawner.FlowerType t)
    {
        owner = spawner;
        type = t;
        phase = Random.Range(0f, Mathf.PI * 2f);
        baseY = transform.position.y;
        born = Time.time;
        float size = spawner.flowerSize;

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);

        if (spawner.glowSprite != null)
        {
            glow = new GameObject("Glow").transform;
            glow.SetParent(visual, false);
            glowSr = glow.gameObject.AddComponent<SpriteRenderer>();
            glowSr.sprite = spawner.glowSprite;
            var c = t.color; c.a = 0.28f;
            glowSr.color = c;
            glowSr.sortingOrder = spawner.sortingOrder - 1;
            Fit(glow, glowSr.sprite, size * 1.9f);
        }

        petals = new GameObject("Petals").transform;
        petals.SetParent(visual, false);
        petalsSr = petals.gameObject.AddComponent<SpriteRenderer>();
        petalsSr.sprite = spawner.petalsSprite;
        petalsSr.color = t.color;
        petalsSr.sortingOrder = spawner.sortingOrder;
        Fit(petals, petalsSr.sprite, size);

        var center = new GameObject("Center").transform;
        center.SetParent(petals, false);
        centerSr = center.gameObject.AddComponent<SpriteRenderer>();
        centerSr.sprite = spawner.centerSprite;
        centerSr.color = FlowerSpawner.CenterColor(t.id);
        centerSr.sortingOrder = spawner.sortingOrder + 1;
        if (centerSr.sprite != null && petalsSr.sprite != null)
        {
            float ps = petalsSr.sprite.bounds.size.x, cs = centerSr.sprite.bounds.size.x;
            if (cs > 0f) center.localScale = Vector3.one * (ps * 0.3f / cs);
        }

        var col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = size * 0.45f;
        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        visual.localScale = Vector3.zero;   // pop-in
    }

    static void Fit(Transform t, Sprite s, float worldSize)
    {
        if (s == null) return;
        float w = s.bounds.size.x;
        if (w > 0f) t.localScale = Vector3.one * (worldSize / w);
    }

    void Update()
    {
        if (collected) return;
        var p = transform.position;
        p.x -= owner.moveSpeed * Time.deltaTime;
        float t = Time.time;
        p.y = baseY + Mathf.Sin(t * 2.2f + phase) * 0.12f;            // ลอยขึ้นลงเบาๆ
        transform.position = p;

        float age = t - born;
        float pop = age < 0.35f ? EaseOutBack(age / 0.35f) : 1f;          // เกิดแบบเด้ง
        float pulse = 1f + Mathf.Sin(t * 4f + phase) * 0.05f;            // เต้นเบาๆ
        visual.localScale = Vector3.one * pop * pulse;
        petals.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.6f + phase) * 14f + t * 25f);   // หมุนช้าๆ + โยก

        if (p.x < owner.KillX) Destroy(gameObject);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;
        if (BFGameManager.instance != null && !BFGameManager.instance.CanCollect) return;
        collected = true;
        GetComponent<Collider2D>().enabled = false;
        if (BFGameManager.instance != null) BFGameManager.instance.CollectFlower(type);
        var bee = other.GetComponentInParent<BeeVisual>();
        if (bee != null) bee.Bump();
        StartCoroutine(CollectFx());
    }

    IEnumerator CollectFx()
    {
        Bits();
        Points();
        float d = 0.35f, t = 0f;
        Vector3 s0 = visual.localScale;
        while (t < d)
        {
            t += Time.deltaTime;
            float k = t / d;
            visual.localScale = s0 * (1f + 0.7f * k);
            float a = 1f - k;
            SetAlpha(petalsSr, a); SetAlpha(centerSr, a); if (glowSr != null) SetAlpha(glowSr, a * 0.28f);
            petals.Rotate(0, 0, 360f * Time.deltaTime);
            yield return null;
        }
        Destroy(gameObject, 0.9f);
        visual.gameObject.SetActive(false);
    }

    static void SetAlpha(SpriteRenderer sr, float a) { if (sr == null) return; var c = sr.color; c.a = a; sr.color = c; }

    // กลีบเล็กๆ กระจายออกรอบดอก
    void Bits()
    {
        var sprite = owner.petalBitSprite != null ? owner.petalBitSprite : owner.petalsSprite;
        if (sprite == null) return;
        for (int i = 0; i < 7; i++)
        {
            var go = new GameObject("Bit");
            go.transform.position = transform.position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = i % 3 == 0 ? Color.white : type.color;
            sr.sortingOrder = owner.sortingOrder + 2;
            float ang = (i / 7f) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            go.AddComponent<FlowerBit>().Init(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(2.2f, 3.4f), owner.flowerSize * 0.28f);
        }
    }

    void Points()
    {
        var go = new GameObject("Points");
        go.transform.position = transform.position + Vector3.up * owner.flowerSize * 0.6f;
        var tmp = go.AddComponent<TextMeshPro>();
        if (owner.font != null) tmp.font = owner.font;
        tmp.text = "+" + type.points;
        tmp.fontSize = 6f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.Lerp(type.color, new Color32(0x6E, 0x5F, 0x58, 255), 0.55f);   // ไม่มีเส้นขอบ
        tmp.sortingOrder = owner.sortingOrder + 3;
        go.AddComponent<FloatingText>();
    }
}

// กลีบเล็กที่กระจาย: พุ่งออก หมุน ค่อยๆ หาย
public class FlowerBit : MonoBehaviour
{
    Vector2 v; float life = 0.6f, t; SpriteRenderer sr; float spin;
    public void Init(Vector2 velocity, float size)
    {
        v = velocity;
        sr = GetComponent<SpriteRenderer>();
        float w = sr.sprite.bounds.size.x;
        if (w > 0f) transform.localScale = Vector3.one * (size / w);
        spin = Random.Range(-540f, 540f);
    }
    void Update()
    {
        t += Time.deltaTime;
        transform.position += (Vector3)(v * Time.deltaTime);
        v *= 1f - 3.5f * Time.deltaTime;
        v.y -= 2f * Time.deltaTime;
        transform.Rotate(0, 0, spin * Time.deltaTime);
        var c = sr.color; c.a = 1f - t / life; sr.color = c;
        if (t >= life) Destroy(gameObject);
    }
}

// ตัวเลข +คะแนน ลอยขึ้นแล้วจาง
public class FloatingText : MonoBehaviour
{
    TMP_Text text; float t;
    void Awake() { text = GetComponent<TMP_Text>(); }
    void Update()
    {
        t += Time.deltaTime;
        transform.position += Vector3.up * 1.4f * Time.deltaTime;
        float s = t < 0.15f ? Mathf.Lerp(0.6f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 1f, (t - 0.15f) / 0.2f);
        transform.localScale = Vector3.one * s;
        if (text != null) text.alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.4f;
        if (t > 0.9f) Destroy(gameObject);
    }
}
