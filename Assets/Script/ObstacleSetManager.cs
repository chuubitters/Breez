using UnityEngine;

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

    [Header("Height Settings")]
    public float minY = -2f;
    public float maxY = 2f;
    // [เพิ่ม] ตัวแปรคุมระยะห่างความสูงระหว่างเสา 2 ต้นติดกัน
    public float maxHeightDifference = 1.5f; 

    [Header("Color Settings")]
    public Color color1 = Color.red;
    public Color color2 = Color.blue;

    private GameObject[] obstacles;
    private float lastY; // เก็บความสูงของเสาต้นล่าสุด

    void Start()
    {
        obstacles = new GameObject[poolSize];
        
        // สุ่มความสูงของเสาต้นแรก
        lastY = Random.Range(minY, maxY);

        for (int i = 0; i < poolSize; i++)
        {
            float posX = startSpawnX + (i * spacingX);
            
            // ตั้งแต่ต้นที่ 2 เป็นต้นไป ให้คำนวณความสูงอิงจากต้นก่อนหน้า
            if (i > 0)
            {
                float randomOffset = Random.Range(-maxHeightDifference, maxHeightDifference);
                // นำความสูงเดิม + ค่าที่สุ่มได้ และบล็อกไม่ให้เกินค่า minY / maxY
                lastY = Mathf.Clamp(lastY + randomOffset, minY, maxY);
            }
            
            obstacles[i] = Instantiate(obstaclePrefab, new Vector3(posX, lastY, 0), Quaternion.identity, transform);
            SetRandomColor(obstacles[i]);
        }
    }

    void Update()
    {
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

        float newX = rightMostX + spacingX;
        
        // สุ่มความสูงใหม่ โดยบวก/ลบ จากต้นที่อยู่ขวาสุดเท่านั้น
        float randomOffset = Random.Range(-maxHeightDifference, maxHeightDifference);
        float newY = Mathf.Clamp(rightMostY + randomOffset, minY, maxY);

        obs.transform.position = new Vector3(newX, newY, obs.transform.position.z);
        SetRandomColor(obs);
    }

    void SetRandomColor(GameObject obs)
    {
        Color pickedColor = (Random.value > 0.5f) ? color1 : color2;

        SpriteRenderer sr = obs.GetComponent<SpriteRenderer>();
        if (sr == null) sr = obs.GetComponentInChildren<SpriteRenderer>();
        
        if (sr != null)
        {
            sr.color = pickedColor;
        }
    }
}