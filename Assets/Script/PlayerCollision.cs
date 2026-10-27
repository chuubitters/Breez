using UnityEngine;

public class PlayerCollision : MonoBehaviour
{
    [Header("ขอบเขตหน้าจอ")]
    public float topBound = 5.5f;    // พิกัดขอบจอบน
    public float bottomBound = -5.5f; // พิกัดขอบจอล่าง

    private bool isDead = false;

    void Update()
    {
        if (isDead) return;

        // เช็คว่าตัวละครหลุดขอบจอบน หรือตกขอบจอล่างหรือไม่
        if (transform.position.y > topBound || transform.position.y < bottomBound)
        {
            Die();
        }
    }

    // ฟังก์ชันนี้จะทำงานเมื่อตัวละครไป "ชน" กับวัตถุที่เป็นแบบแข็ง (ไม่ได้ติ๊ก Is Trigger)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        // เช็คว่าวัตถุที่ชนมี Tag ชื่อ Obstacle หรือไม่
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        
        // สั่งให้ GameManager แสดงหน้าจอ Game Over และหยุดเวลา
        if (BFGameManager.instance != null)
        {
            BFGameManager.instance.GameOver();
        }
    }
}