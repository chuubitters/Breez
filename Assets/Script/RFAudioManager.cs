using UnityEngine;

public class RFAudioManager : MonoBehaviour
{
    public AudioSource bgm;
    public AudioSource sfx;
    
    // เพิ่มตัวแปรสำหรับใส่ Particle System (เอฟเฟกต์ฝน)
    public ParticleSystem rainParticles; 

    void Start()
    {
        // เพลงที่ 1 เล่นเมื่อเปิดเกม
        if (bgm != null)
        {
            bgm.Play();
        }

        // ทำให้แน่ใจว่าฝนไม่ได้ตกตั้งแต่เริ่มเกม
        if (rainParticles != null)
        {
            rainParticles.Stop();
        }
    }

    void Update()
    {
        // กดลูกศรขึ้น = เริ่มเสียง 2 และให้ฝนตก
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (sfx != null) sfx.Play();
            
            if (rainParticles != null) 
            {
                rainParticles.Play(); // เริ่มร่วง
            }
        }

        // ปล่อยลูกศรขึ้น = หยุดเสียง 2 และหยุดฝน
        if (Input.GetKeyUp(KeyCode.UpArrow))
        {
            if (sfx != null) sfx.Stop();
            
            if (rainParticles != null) 
            {
                rainParticles.Stop(); // หยุดร่วง
            }
        }
    }
}