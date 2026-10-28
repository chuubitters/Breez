using UnityEngine;

// =====================================================================
// ฝนเม็ดเล็ก (drizzle) สำหรับ Rainy Forest
// ใส่ไว้ที่ GameObject "RainEffect" ที่มี ParticleSystem (เมนู Breez > Upgrade Rainy Forest Rain)
// - ตอนเริ่มจะตั้งค่า ParticleSystem ใหม่ให้เป็นเส้นฝนเล็กๆ (ไม่ใช้รูปเม็ดฝนใหญ่แบบเดิม)
// - SPGameManager ยังสั่ง Play/Stop เหมือนเดิม (เป่า = ฝนตก)
// - เป่าแรง = ฝนตกหนักขึ้น
// =====================================================================
[RequireComponent(typeof(ParticleSystem))]
public class BreathRain : MonoBehaviour
{
    [Header("Breath")]
    public BreathReceiver breathReceiver;
    [Range(0, 100)] public int threshold = 50;

    [Header("Rain")]
    public float lightRate = 220f;       // เม็ด/วินาที ตอนเป่าเบา
    public float heavyRate = 900f;       // เม็ด/วินาที ตอนเป่าแรงสุด
    public Vector2 dropSize = new Vector2(0.025f, 0.05f);
    public Vector2 fallSpeed = new Vector2(11f, 15f);
    [Tooltip("ลมพัดฝนเฉียง (หน่วย/วินาที แกน X)")]
    public float wind = -1.2f;
    public Color dropColor = new Color(0.76f, 0.85f, 0.96f, 0.75f);
    [Tooltip("ความยาวเส้นฝน")]
    public float streak = 0.025f;

    ParticleSystem ps;
    float shownRate;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        if (breathReceiver == null) breathReceiver = FindObjectOfType<BreathReceiver>();
        Configure();
    }

    void Configure()
    {
        var cam = Camera.main;
        float halfH = cam != null && cam.orthographic ? cam.orthographicSize : 5f;
        float halfW = halfH * (cam != null ? cam.aspect : 16f / 9f);
        Vector3 c = cam != null ? cam.transform.position : Vector3.zero;

        // ย้ายไปไว้เหนือขอบจอบน (คงค่า z เดิมไว้ให้ลำดับการวาดเหมือนเดิม)
        transform.position = new Vector3(c.x + 1.5f, c.y + halfH + 0.6f, transform.position.z);
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(fallSpeed.x, fallSpeed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(dropSize.x, dropSize.y);
        main.startRotation = 0f;
        main.startColor = dropColor;
        main.gravityModifier = 0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 4000;
        main.prewarm = false;

        var tsa = ps.textureSheetAnimation;
        tsa.enabled = false;                                  // เลิกใช้รูปเม็ดฝนใหญ่

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = Vector3.zero;
        shape.rotation = new Vector3(90f, 0f, 0f);           // ยิงลงล่าง
        shape.scale = new Vector3(halfW * 2f + 4f, 0.2f, 0.1f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(wind);
        vel.y = new ParticleSystem.MinMaxCurve(0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var size = ps.sizeOverLifetime; size.enabled = false;
        var rot = ps.rotationOverLifetime; rot.enabled = false;
        var noise = ps.noise; noise.enabled = false;

        var r = GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;      // เป็นเส้นตามทิศที่ตก
        r.velocityScale = streak;
        r.lengthScale = 1.5f;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = lightRate;
        shownRate = lightRate;
    }

    void Update()
    {
        if (ps == null || !ps.isEmitting) return;
        int level = breathReceiver != null ? breathReceiver.breathLevel : 0;
        bool arrow = Input.GetKey(KeyCode.UpArrow);
        float k = arrow ? 0.7f : Mathf.InverseLerp(threshold, 100f, level);
        float target = Mathf.Lerp(lightRate, heavyRate, k);
        shownRate = Mathf.Lerp(shownRate, target, 1f - Mathf.Exp(-Time.deltaTime * 4f));
        var em = ps.emission;
        em.rateOverTime = shownRate;
    }
}
