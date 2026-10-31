using System;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

// =====================================================================
// Breathe & Focus (แบบเก็บดอกไม้): สร้างดอกไม้ลอยเข้ามาจากขวาไปซ้าย
//   • ดอกไม้ 3 สี คะแนนต่างกัน (ชมพู / เหลือง / ม่วง)
//   • ความสูงของดอกถัดไปเปลี่ยนทีละน้อย ผู้เล่นตามเก็บได้ด้วยการเป่า/หยุดเป่า
//   • ดอกสีม่วง (คะแนนสูงสุด) อยู่ใกล้ขอบบน/ล่าง ต้องควบคุมลมหายใจดีถึงจะเก็บได้
// ตั้งค่า sprite / font ให้อัตโนมัติจากเมนู Breez > Breathe & Focus: ผึ้งเก็บดอกไม้
// =====================================================================
public class FlowerSpawner : MonoBehaviour
{
    [Serializable]
    public class FlowerType
    {
        public string id = "pink";
        public string label = "ดอกสีชมพู";
        public Color color = new Color32(0xF2, 0xA6, 0xBC, 255);
        public int points = 10;
        [Range(0f, 1f)] public float weight = 0.6f;
    }

    public FlowerType[] types =
    {
        new FlowerType { id = "pink",   label = "ดอกสีชมพู",  color = new Color32(0xF2, 0xA6, 0xBC, 255), points = 10, weight = 0.6f },
        new FlowerType { id = "yellow", label = "ดอกสีเหลือง", color = new Color32(0xF6, 0xCF, 0x4A, 255), points = 20, weight = 0.3f },
        new FlowerType { id = "purple", label = "ดอกสีม่วง",  color = new Color32(0xA8, 0x8B, 0xD8, 255), points = 50, weight = 0.1f },
    };

    [Header("ภาพ (ตั้งจากเมนู)")]
    public Sprite petalsSprite;     // ดอกสีขาว ย้อมสีตามชนิด
    public Sprite centerSprite;     // เกสรกลางดอก
    public Sprite glowSprite;       // แสงฟุ้งด้านหลัง
    public Sprite petalBitSprite;   // กลีบเล็กที่กระจายตอนเก็บ
    public TMP_FontAsset font;      // ตัวเลข +คะแนน

    [Header("การเกิด")]
    public float spawnInterval = 1.15f;
    public float moveSpeed = 3.6f;
    public float flowerSize = 0.9f;          // ขนาด (world unit)
    [Tooltip("ระยะห่างจากขอบบน/ล่างของจอ")]
    public float edgeMargin = 0.9f;
    [Tooltip("ความสูงเปลี่ยนได้มากสุดต่อดอก")]
    public float maxStep = 1.7f;
    public int sortingOrder = 5;

    float timer, lastY, top, bottom, spawnX, killX;
    bool running;

    public void Begin() { running = true; timer = 0.3f; }
    public void Stop() { running = false; }

    void Start()
    {
        var cam = Camera.main;
        float half = cam != null && cam.orthographic ? cam.orthographicSize : 5f;
        float cy = cam != null ? cam.transform.position.y : 0f;
        float cx = cam != null ? cam.transform.position.x : 0f;
        float aspect = cam != null ? cam.aspect : 16f / 9f;
        top = cy + half - edgeMargin;
        bottom = cy - half + edgeMargin;
        spawnX = cx + half * aspect + flowerSize;
        killX = cx - half * aspect - flowerSize * 2f;
        lastY = cy;
    }

    void Update()
    {
        if (!running) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = spawnInterval * Random.Range(0.85f, 1.15f);
        Spawn();
    }

    void Spawn()
    {
        var type = PickType();
        float y;
        if (type == types[types.Length - 1])
        {
            // ดอกคะแนนสูงสุด: ชิดขอบบนหรือล่าง
            y = Random.value < 0.5f ? Random.Range(top - 0.6f, top) : Random.Range(bottom, bottom + 0.6f);
            y = Mathf.Clamp(y, lastY - maxStep * 1.6f, lastY + maxStep * 1.6f);
        }
        else
        {
            y = Mathf.Clamp(lastY + Random.Range(-maxStep, maxStep), bottom, top);
        }
        lastY = Mathf.Clamp(y, bottom, top);

        var go = new GameObject("Flower_" + type.id);
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(spawnX, lastY, 0f);
        var f = go.AddComponent<FlowerPickup>();
        f.Init(this, type);
    }

    FlowerType PickType()
    {
        float sum = 0f;
        foreach (var t in types) sum += t.weight;
        float r = Random.value * sum;
        foreach (var t in types) { if (r < t.weight) return t; r -= t.weight; }
        return types[0];
    }

    public float KillX => killX;
}
