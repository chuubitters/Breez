using UnityEngine;

// =====================================================================
// Breathe & Focus: ก้อนเมฆสิ่งกีดขวาง
// โหมดเมฆเดี่ยว (singleCloudMode, ค่าเริ่มต้น):
//   แต่ละชุดแสดงเมฆก้อนเดียว สุ่มอยู่ "บน" / "ล่าง" (บางครั้ง "กลาง")
//   ผู้เล่นต้องขยับขึ้นลง ไม่ได้อยู่กลางจอตลอด และเมฆห่างกันมากขึ้น (cloudSpacingX)
//   ไม่ให้ตำแหน่งเดิมซ้ำเกิน 2 ครั้งติด
// ปิด singleCloudMode = กลับไปเป็นเมฆคู่บน-ล่างแบบเดิม
// =====================================================================
public class ObstacleSetManager : MonoBehaviour
{
    [Header("Obstacle Settings")]
    public GameObject obstaclePrefab;
    public int poolSize = 5;
    public float spacingX = 4f;
    public float moveSpeed = 4f;

    [Header("Loop Boundaries")]
    public float resetX = -10f;
    public float startSpawnX = 10f;

    [Header("Height Settings (เมฆคู่แบบเดิม)")]
    public float minY = -2f;
    public float maxY = 2f;
    // ตัวแปรคุมระยะห่างความสูงระหว่างเสา 2 ต้นติดกัน
    public float maxHeightDifference = 1.5f;

    [Header("เมฆเดี่ยว บน/ล่าง")]
    public bool singleCloudMode = true;
    [Tooltip("ระยะห่างแนวนอนระหว่างเมฆ (โหมดเมฆเดี่ยว)")]
    public float cloudSpacingX = 7.5f;
    [Tooltip("ความสูง (world Y) ของเมฆด้านบน: สุ่มระหว่าง x..y")]
    public Vector2 topCloudY = new Vector2(1.7f, 3.1f);
    [Tooltip("ความสูง (world Y) ของเมฆด้านล่าง: สุ่มระหว่าง x..y")]
    public Vector2 bottomCloudY = new Vector2(-3.1f, -1.7f);
    [Tooltip("โอกาสเกิดเมฆกลางจอ (ต้องหลบขึ้นหรือลง)")]
    [Range(0f, 1f)] public float middleChance = 0.15f;
    public Vector2 middleCloudY = new Vector2(-0.5f, 0.5f);

    [Header("Color Settings")]
    public Color color1 = Color.red;
    public Color color2 = Color.blue;

    private GameObject[] obstacles;
    private float lastY; // เก็บความสูงของเสาต้นล่าสุด
    private int lastLane = -1, sameLaneCount;

    float Spacing => singleCloudMode ? cloudSpacingX : spacingX;

    void Start()
    {
        obstacles = new GameObject[poolSize];

        // สุ่มความสูงของเสาต้นแรก
        lastY = Random.Range(minY, maxY);

        for (int i = 0; i < poolSize; i++)
        {
            float posX = startSpawnX + (i * Spacing);

            // ตั้งแต่ต้นที่ 2 เป็นต้นไป ให้คำนวณความสูงอิงจากต้นก่อนหน้า
            if (i > 0)
            {
                float randomOffset = Random.Range(-maxHeightDifference, maxHeightDifference);
                lastY = Mathf.Clamp(lastY + randomOffset, minY, maxY);
            }

            obstacles[i] = Instantiate(obstaclePrefab, new Vector3(posX, lastY, 0), Quaternion.identity, transform);
            if (singleCloudMode) ArrangeSingle(obstacles[i]);
            SetRandomColor(obstacles[i]);
        }
    }

    void Update()
    {
        // ระยะเลื่อนกลับต้องพ้นทั้งหมด (pool * spacing) ไม่งั้นเมฆซ้อนกัน
        for (int i = 0; i < poolSize; i++)
        {
            obstacles[i].transform.Translate(Vector3.left * moveSpeed * Time.deltaTime);

            if (obstacles[i].transform.position.x <= resetX)
            {
                RepositionToBack(obstacles[i]);
            }
        }
    }

    void RepositionToBack(GameObject obs)
    {
        float rightMostX = resetX;
        float rightMostY = 0f;

        // หาว่า ณ ตอนนี้ เสาต้นไหนอยู่ขวาสุด และความสูงของต้นนั้นคือเท่าไหร่
        for (int i = 0; i < poolSize; i++)
        {
            if (obstacles[i].transform.position.x > rightMostX)
            {
                rightMostX = obstacles[i].transform.position.x;
                rightMostY = obstacles[i].transform.position.y;
            }
        }

        float newX = rightMostX + Spacing;

        // สุ่มความสูงใหม่ โดยบวก/ลบ จากต้นที่อยู่ขวาสุดเท่านั้น
        float randomOffset = Random.Range(-maxHeightDifference, maxHeightDifference);
        float newY = Mathf.Clamp(rightMostY + randomOffset, minY, maxY);

        obs.transform.position = new Vector3(newX, newY, obs.transform.position.z);
        if (singleCloudMode) ArrangeSingle(obs);
        SetRandomColor(obs);
    }

    // ---------------------------------------------------------------
    // เมฆเดี่ยว: เปิดเฉพาะ TopPillar หรือ BottomPillar แล้วเลื่อนชุดให้เมฆอยู่ที่ความสูงที่สุ่ม
    // (ใช้ TopPillar เป็นเมฆตรงกลางในเลนกลาง)
    // ---------------------------------------------------------------
    void ArrangeSingle(GameObject obs)
    {
        var t = obs.transform;
        var top = t.Find("TopPillar");
        var bottom = t.Find("BottomPillar");
        if (top == null || bottom == null) return;

        int lane = PickLane();   // 0 บน, 1 ล่าง, 2 กลาง
        var show = lane == 1 ? bottom : top;
        var hide = lane == 1 ? top : bottom;
        show.gameObject.SetActive(true);
        hide.gameObject.SetActive(false);

        Vector2 range = lane == 0 ? topCloudY : lane == 1 ? bottomCloudY : middleCloudY;
        float targetY = Random.Range(range.x, range.y);
        float offset = show.position.y - t.position.y;      // ระยะเมฆจากจุดกลางชุด
        var p = t.position;
        t.position = new Vector3(p.x, targetY - offset, p.z);

        // ทริกเกอร์นับคะแนน: ให้สูงเต็มจอ ผ่านตรงไหนก็ได้คะแนน
        var score = t.Find("ScoreTrigger");
        if (score != null)
        {
            var box = score.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                float sy = Mathf.Abs(score.lossyScale.y) > 0.0001f ? Mathf.Abs(score.lossyScale.y) : 1f;
                box.size = new Vector2(box.size.x, 30f / sy);
                box.offset = new Vector2(box.offset.x, -score.position.y / sy);
            }
        }
    }

    int PickLane()
    {
        int lane;
        if (Random.value < middleChance) lane = 2;
        else lane = Random.value < 0.5f ? 0 : 1;
        if (lane == lastLane && sameLaneCount >= 2) lane = lane == 0 ? 1 : 0;   // ไม่ซ้ำเกิน 2 ครั้งติด
        sameLaneCount = lane == lastLane ? sameLaneCount + 1 : 1;
        lastLane = lane;
        return lane;
    }

    void SetRandomColor(GameObject obs)
    {
        Color pickedColor = (Random.value > 0.5f) ? color1 : color2;

        SpriteRenderer sr = obs.GetComponent<SpriteRenderer>();
        if (sr == null) sr = obs.GetComponentInChildren<SpriteRenderer>();   // ก้อนที่แสดงอยู่

        if (sr != null)
        {
            sr.color = pickedColor;
        }
    }
}
