using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================
// Breez UI animation (ทำงานเองทุก scene ไม่ต้องตั้งค่า)
//   • ทุกปุ่ม: ชี้เมาส์ = ขยายเล็กน้อย / กด = ยุบ / ปล่อย = เด้งกลับ
//   • หน้าต่าง (วิธีการเล่น / ผลลัพธ์ / สรุป / พัก / ประวัติ): เปิดแล้วค่อยๆ ขึ้น + เด้งเบาๆ และอยู่บนสุดของ Canvas
//   ปุ่มที่ไม่อยากให้ขยับ: ใส่ component BreezNoFx
// =====================================================================
public static class BreezUiFx
{
    /// <summary>ชื่อหน้าต่างที่ต้องอยู่บนสุดเมื่อเปิด</summary>
    public static readonly HashSet<string> PanelNames = new HashSet<string>
    {
        "HowToPlayPanel", "ResultPanel", "SummaryPanel", "PausePanel", "GameOverPanel", "DailyHistory"
    };

    static int lastScene = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded += (s, m) => { if (m == LoadSceneMode.Single) Apply(s); };
        Apply(SceneManager.GetActiveScene());
    }

    static void Apply(Scene scene)
    {
        if (!scene.IsValid() || scene.handle == lastScene) return;
        lastScene = scene.handle;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                if (b.GetComponent<BreezNoFx>() != null || b.GetComponent<Animator>() != null) continue;
                if (b.GetComponent<BreezButtonFx>() == null) b.gameObject.AddComponent<BreezButtonFx>();
            }
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
            {
                bool topChild = t.parent != null && t.parent.name == "BreezTopCanvas";
                if (!PanelNames.Contains(t.name) && !topChild) continue;
                var target = t;
                // DailyHistory: ตัวหน้าต่างจริงคือลูกชื่อ Panel (ตัวแม่เปิดค้างไว้ตลอด)
                if (t.name == "DailyHistory") { var p = t.Find("Panel") as RectTransform; if (p == null) continue; target = p; }
                if (target.GetComponent<BreezPanelFx>() != null || target.GetComponent<BreezNoFx>() != null) continue;
                if (target.GetComponent<TMPro.TMP_Text>() != null) continue;
                if (target.GetComponent<SoundscapeResultUI>() != null || target.GetComponent<SoundscapeCountdownUI>() != null) continue;
                target.gameObject.AddComponent<BreezPanelFx>();
            }
        }
    }
}
