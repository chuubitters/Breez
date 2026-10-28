using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// Windy Mountain: เงาดอกแดนดิไลออน
// - เป่า (ESP32 >= threshold) หรือกดลูกศรขึ้น => เมล็ดหลุดปลิวไปตามลม ต้นโยกตามแรงเป่า
// - หยุดเป่าสักพัก => เมล็ดค่อยๆ งอกกลับมา (เป่าได้เรื่อยๆ)
// - ทำงานหลังกด Start (SPGameManager.IsPlaying) เท่านั้น
// สร้างพร้อมฉากด้วยเมนู Breez > Create Windy Mountain Scene
// โครงสร้าง: Dandelion (โยก) > Head > Seed* (SpriteRenderer ชื่อขึ้นต้นด้วย "Seed")
// =====================================================================
public class DandelionField : MonoBehaviour
{
    [Header("Breath")]
    public BreathReceiver breathReceiver;
    [Range(0, 100)] public int threshold = 50;
    public bool arrowKeyBlows = true;
    [Tooltip("ทำงานก่อนกด Start ด้วยไหม")]
    public bool workBeforeStart = false;

    [Header("Dandelions")]
    public Transform[] dandelions;
    public float swayDegrees = 9f;

    [Header("Seeds")]
    public float seedsPerSecond = 16f;
    public Vector2 windDirection = new Vector2(1f, 0.35f);
    public float windSpeed = 3.2f;
    public float seedLifetime = 6f;
    public float regrowDelay = 2.5f;
    public float regrowPerSecond = 8f;

    /// <summary>แรงเป่า 0-1 (smooth แล้ว)</summary>
    public float Level01 { get; private set; }
    public int SeedsAttached { get; private set; }

    class Seed
    {
        public Transform t, head;
        public SpriteRenderer sr;
        public Vector3 homePos, homeScale;
        public Quaternion homeRot;
        public Color color;
        public int state;          // 0 ติดดอก, 1 ปลิว, 2 หายไป, 3 กำลังงอก
        public Vector2 vel;
        public float age, phase, spin;
    }

    readonly List<Seed> seeds = new List<Seed>();
    float[] baseAngles;
    float detachAcc, regrowAcc, idleTime;
    SPGameManager game;
    Camera cam;

    void Awake()
    {
        if (breathReceiver == null) breathReceiver = FindObjectOfType<BreathReceiver>();
        game = FindObjectOfType<SPGameManager>();
        cam = Camera.main;

        if (dandelions == null || dandelions.Length == 0)
        {
            var list = new List<Transform>();
            foreach (Transform child in transform) if (child.name.StartsWith("Dandelion")) list.Add(child);
            dandelions = list.ToArray();
        }

        baseAngles = new float[dandelions.Length];
        for (int i = 0; i < dandelions.Length; i++)
        {
            baseAngles[i] = dandelions[i].localEulerAngles.z;
            foreach (var sr in dandelions[i].GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!sr.name.StartsWith("Seed")) continue;
                seeds.Add(new Seed
                {
                    t = sr.transform, head = sr.transform.parent, sr = sr,
                    homePos = sr.transform.localPosition, homeRot = sr.transform.localRotation,
                    homeScale = sr.transform.localScale, color = sr.color,
                    phase = Random.value * 10f,
                });
            }
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        bool playing = workBeforeStart || game == null || game.IsPlaying;

        int level = breathReceiver != null ? breathReceiver.breathLevel : 0;
        bool arrow = arrowKeyBlows && Input.GetKey(KeyCode.UpArrow);
        bool blowing = playing && (arrow || level >= threshold);
        float target = !blowing ? 0f : arrow ? 0.75f : Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(threshold, 100f, level));
        Level01 = Mathf.MoveTowards(Level01, target, dt * (target > Level01 ? 4f : 1.5f));

        // ต้นโยกตามแรงลม
        for (int i = 0; i < dandelions.Length; i++)
        {
            float idle = Mathf.Sin(Time.time * 0.9f + i * 1.7f) * 1.2f;
            float gust = Level01 * swayDegrees * (0.8f + 0.2f * Mathf.Sin(Time.time * 6f + i));
            dandelions[i].localRotation = Quaternion.Euler(0, 0, baseAngles[i] + idle - gust * Mathf.Sign(windDirection.x));
        }

        // เมล็ดหลุด
        if (blowing)
        {
            idleTime = 0f;
            detachAcc += seedsPerSecond * (0.3f + Level01) * dt;
            while (detachAcc >= 1f) { detachAcc -= 1f; Detach(PickRandom(0)); }
        }
        else idleTime += dt;

        // เมล็ดงอกกลับ
        if (!blowing && idleTime > regrowDelay)
        {
            regrowAcc += regrowPerSecond * dt;
            while (regrowAcc >= 1f) { regrowAcc -= 1f; Regrow(PickRandom(2)); }
        }

        UpdateSeeds(dt);
    }

    void UpdateSeeds(float dt)
    {
        float halfH = cam != null ? cam.orthographicSize : 5f;
        float halfW = halfH * (cam != null ? cam.aspect : 1.78f);
        Vector3 c = cam != null ? cam.transform.position : Vector3.zero;
        Vector2 dir = windDirection.normalized;
        int attached = 0;

        foreach (var s in seeds)
        {
            switch (s.state)
            {
                case 0:
                    attached++;
                    break;

                case 1:
                    s.age += dt;
                    var windVel = dir * windSpeed * (0.45f + Level01 * 1.4f);
                    var targetVel = windVel + new Vector2(0f, 0.45f * Mathf.Sin(Time.time * 2.1f + s.phase));
                    s.vel = Vector2.Lerp(s.vel, targetVel, 1f - Mathf.Exp(-dt * 1.4f));
                    s.t.position += (Vector3)(s.vel * dt);
                    s.t.Rotate(0f, 0f, (s.spin + Mathf.Sin(Time.time * 3f + s.phase) * 35f) * dt);

                    float fade = 1f - Mathf.InverseLerp(seedLifetime * 0.6f, seedLifetime, s.age);
                    var col = s.color; col.a *= fade; s.sr.color = col;

                    var p = s.t.position;
                    bool off = p.x > c.x + halfW + 1f || p.x < c.x - halfW - 1f || p.y > c.y + halfH + 1f;
                    if (fade <= 0f || off) { s.state = 2; s.sr.enabled = false; }
                    break;

                case 3:
                    s.age += dt;
                    float f = Mathf.Clamp01(s.age / 0.6f);
                    float e = 1f - (1f - f) * (1f - f);
                    s.t.localScale = s.homeScale * e;
                    var cc = s.color; cc.a *= e; s.sr.color = cc;
                    if (f >= 1f) s.state = 0;
                    break;
            }
        }
        SeedsAttached = attached;
    }

    Seed PickRandom(int state)
    {
        int count = 0;
        foreach (var s in seeds) if (s.state == state) count++;
        if (count == 0) return null;
        int k = Random.Range(0, count);
        foreach (var s in seeds) if (s.state == state && k-- == 0) return s;
        return null;
    }

    void Detach(Seed s)
    {
        if (s == null) return;
        Vector2 outward = (s.t.position - s.head.position).normalized;
        s.t.SetParent(null, true);
        s.state = 1;
        s.age = 0f;
        s.spin = Random.Range(-60f, 60f);
        s.vel = outward * Random.Range(0.6f, 1.4f) + windDirection.normalized * 0.8f;
    }

    void Regrow(Seed s)
    {
        if (s == null) return;
        s.t.SetParent(s.head, false);
        s.t.localPosition = s.homePos;
        s.t.localRotation = s.homeRot;
        s.t.localScale = Vector3.zero;
        var cc = s.color; cc.a = 0f; s.sr.color = cc;
        s.sr.enabled = true;
        s.state = 3;
        s.age = 0f;
    }
}
