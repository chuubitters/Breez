using UnityEngine;

public class ScoreTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // เช็คว่าคนที่มาชนคือผู้เล่น (อย่าลืมตั้ง Tag ของ Player เป็น "Player" ด้วย)
        if (collision.gameObject.CompareTag("Player"))
        {
            // เรียกใช้ AddScore ใน BFGameManager ของคุณได้เลย!
            BFGameManager.instance.AddScore();
        }
    }
}