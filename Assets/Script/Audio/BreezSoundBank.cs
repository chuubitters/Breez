using System;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// รายการเสียงทั้งหมดของเกม (Assets/Resources/BreezSoundBank.asset)
//   sounds     : id -> AudioClip (+ ความดัง)  เช่น ui_click, balloon_pop, bgm_menu
//   sceneMusic : ชื่อ scene -> id ของเพลงประกอบ ("" = ไม่เล่นเพลงกลาง ให้ scene จัดการเอง)
// อยากเปลี่ยนเสียง: ลากไฟล์เสียงใหม่ใส่ช่อง clip ของ id นั้นได้เลย (ไม่ต้องแก้โค้ด)
// =====================================================================
[CreateAssetMenu(fileName = "BreezSoundBank", menuName = "Breez/Sound Bank")]
public class BreezSoundBank : ScriptableObject
{
    [Serializable]
    public class Sound
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Serializable]
    public class SceneMusic
    {
        public string scene;
        [Tooltip("id ของเพลงใน sounds  (ว่าง = หยุดเพลงกลาง)")]
        public string music;
    }

    [Tooltip("เพลงสำหรับ scene ที่ไม่ได้ระบุไว้")]
    public string defaultMusic = "bgm_menu";
    public List<SceneMusic> sceneMusic = new List<SceneMusic>();
    public List<Sound> sounds = new List<Sound>();

    public Sound Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var s in sounds) if (s != null && s.id == id) return s;
        return null;
    }

    /// <summary>คืน id ของเพลงสำหรับ scene (null = ไม่ได้ระบุ)</summary>
    public string MusicFor(string scene)
    {
        foreach (var m in sceneMusic)
            if (m != null && string.Equals(m.scene, scene, StringComparison.OrdinalIgnoreCase)) return m.music ?? "";
        return null;
    }
}
