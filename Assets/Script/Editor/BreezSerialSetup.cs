using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// =====================================================================
// เมนู Breez > Configure Serial for ESP32
// เปิดทุก scene ใน Build Settings (และ scene เกมหลัก) แล้ว:
//   - ตั้ง SerialController: baudRate 115200, dtrEnable = true
//   - ใส่ SerialPortAutoSelect ให้หาพอร์ต ESP32 เอง (ไม่ต้องแก้ portName ทุก scene)
// เมนู Breez > Show Serial Ports : แสดงพอร์ตที่เจอตอนนี้
// =====================================================================
public static class BreezSerialSetup
{
    static readonly string[] ExtraScenes =
    {
        "Assets/Scenes/BreathFocus.unity", "Assets/Scenes/PowerRelease.unity",
        "Assets/Scenes/Rainy Forest.unity", "Assets/Scenes/GadgetSetting.unity",
    };

    [MenuItem("Breez/Configure Serial for ESP32")]
    public static void Configure()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = EditorSceneManager.GetActiveScene().path;

        var paths = new List<string>();
        foreach (var s in EditorBuildSettings.scenes) if (!paths.Contains(s.path)) paths.Add(s.path);
        foreach (var s in ExtraScenes) if (!paths.Contains(s)) paths.Add(s);

        var report = new StringBuilder();
        int changed = 0;
        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int inScene = 0;

            foreach (var mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (mb == null || mb.gameObject.scene != scene || mb.GetType().Name != "SerialController") continue;

                var so = new SerializedObject(mb);
                var baud = so.FindProperty("baudRate");
                if (baud != null) baud.intValue = 115200;
                var dtr = so.FindProperty("dtrEnable");
                if (dtr != null) dtr.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                if (mb.GetComponent<SerialPortAutoSelect>() == null)
                    mb.gameObject.AddComponent<SerialPortAutoSelect>();
                EditorUtility.SetDirty(mb.gameObject);
                inScene++;
            }

            if (inScene > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.AppendLine($"✓ {Path.GetFileNameWithoutExtension(path)} ({inScene})");
                changed += inScene;
            }
        }

        if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

        var ports = SerialPortAutoSelect.ListCandidatePorts();
        EditorUtility.DisplayDialog("Breez — Serial for ESP32",
            (changed > 0 ? "Updated SerialController in:\n" + report : "No SerialController found in any scene.\n") +
            "\nBaud 115200, DTR on, port auto-detected at Play.\n\n" +
            (ports.Count > 0 ? "Ports found now:\n" + string.Join("\n", ports) : "No USB serial port found now — plug in the ESP32 and press Play."),
            "OK");
    }

    [MenuItem("Breez/Show Serial Ports")]
    public static void ShowPorts()
    {
        var ports = SerialPortAutoSelect.ListCandidatePorts();
        EditorUtility.DisplayDialog("Breez — Serial ports",
            ports.Count > 0
                ? "Found (first one is used):\n" + string.Join("\n", ports) + "\n\nIf the wrong one is picked, close Arduino IDE Serial Monitor and unplug other boards."
                : "No USB serial port found.\n\n• Plug the ESP32-C3 in with a data cable\n• Close Arduino IDE's Serial Monitor (only one app can open the port)",
            "OK");
    }
}
