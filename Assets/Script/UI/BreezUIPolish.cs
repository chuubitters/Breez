using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================
// ทำงานเองทุก scene (ไม่ต้องกดเมนู):
//   • ทุกปุ่ม: แอนิเมชันตอนชี้/กด (BreezButtonFx)
//   • แผงวิธีการเล่น / ผลลัพธ์ / พัก / สรุป: ปรากฏนุ่มๆ + อยู่บนสุดเสมอ (BreezPanelPop)
// =====================================================================
public static class BreezUIPolish
{
    static readonly string[] PanelNames =
    {
        "HowToPlayPanel", "ResultPanel", "SoundscapeResult", "SummaryPanel", "PausePanel", "GameOverPanel",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(scene);

    public static void Apply(Scene scene)
    {
        if (!scene.IsValid()) return;
        var panels = new HashSet<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var b in root.GetComponentsInChildren<Button>(true))
                if (b.GetComponent<BreezButtonFx>() == null && b.GetComponentInParent<BreezNoFx>(true) == null)
                    b.gameObject.AddComponent<BreezButtonFx>();

            foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
                foreach (var n in PanelNames)
                    if (t.name == n) { panels.Add(t.gameObject); break; }
        }

        // แผงที่ GameManager อ้างถึง
        foreach (var gm in Object.FindObjectsOfType<PWGameManager>(true)) { panels.Add(gm.howToPanel); panels.Add(gm.gameOverPanel); }
        foreach (var gm in Object.FindObjectsOfType<BFGameManager>(true)) { panels.Add(gm.startPanel); panels.Add(gm.gameOverPanel); }
        foreach (var gm in Object.FindObjectsOfType<CalmFlowGame>(true)) { panels.Add(gm.howToPanel); panels.Add(gm.summaryPanel); panels.Add(gm.pausePanel); }
        foreach (var gm in Object.FindObjectsOfType<SPGameManager>(true)) { if (gm.resultUI != null) panels.Add(gm.resultUI.gameObject); }

        foreach (var p in panels)
        {
            if (p == null || p.scene != scene || p.transform.GetComponentInParent<BreezNoFx>(true) != null) continue;
            if (p.GetComponent<SoundscapeResultUI>() != null) continue;   // มีแอนิเมชันของตัวเองแล้ว (อยู่ใน BreezTopCanvas)
            if (p.GetComponent<BreezPanelPop>() == null) p.AddComponent<BreezPanelPop>();
        }
    }
}
