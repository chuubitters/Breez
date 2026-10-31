using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// =====================================================================
// Breez > Breathe & Focus: ผึ้งเก็บดอกไม้  (กดซ้ำได้)
//   1) ตัวละครเปลี่ยนเป็นผึ้ง (กระพือปีก เอียงตัว) ไม่ตกขอบจอ ไม่มีการชนแล้วแพ้
//   2) ปิดก้อนเมฆสิ่งกีดขวาง แล้วเพิ่ม FlowerSpawner (ดอกไม้ 3 สี คะแนนต่างกัน)
//   3) เล่น 60 วินาที หมดเวลาแสดงผลลัพธ์: คะแนน / เวลา / ดอกไม้ทั้งหมด + แยกสี
//   4) จัดหน้าผลลัพธ์ (Restyle) และหน้าวิธีการเล่นใหม่ให้ตรงกับเกม
// =====================================================================
public static class BreezBreathFocusBee
{
    const string ScenePath = "Assets/Scenes/BreathFocus.unity";
    const float BodyPpu = 200f, WingPpu = 200f;

    public static Sprite BodySprite, WingSprite, PetalsSprite, CenterSprite;
    const string FlowerFolder = "Assets/element/B&F/Flowers";
    static readonly string[] FlowerFiles = { "flower_pink", "flower_yellow", "flower_clover" };   // ตามลำดับ FlowerSpawner.DefaultTypes

    /// <summary>ภาพดอกไม้สีจริง 3 แบบ (ชมพู / เหลือง / โคลเวอร์)</summary>
    public static Sprite FlowerSprite(int i)
    {
        string path = $"{FlowerFolder}/{FlowerFiles[i]}.png";
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return null;
        if (imp.textureType != TextureImporterType.Sprite || imp.mipmapEnabled || !imp.alphaIsTransparency)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.spritePixelsPerUnit = 256f;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static FlowerSpawner.FlowerType[] TypesWithSprites()
    {
        var types = FlowerSpawner.DefaultTypes();
        for (int i = 0; i < types.Length && i < FlowerFiles.Length; i++) types[i].sprite = FlowerSprite(i);
        return types;
    }

    [MenuItem("Breez/Breathe & Focus: ผึ้งเก็บดอกไม้")]
    public static void Run()
    {
        if (!File.Exists(ScenePath)) { EditorUtility.DisplayDialog("Breez", "ไม่พบ " + ScenePath, "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureArt();

        string msg = Convert();
        if (msg != null) { EditorUtility.DisplayDialog("Breez", msg, "OK"); return; }
        BreezBreathFocusRestyle.Build(true);   // หน้าผลลัพธ์แบบดอกไม้
        BreezBreathPlayLayers.FocusOnly();     // หน้าวิธีการเล่น + ย้ายขึ้นชั้นบนสุด
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Breez",
            "เปลี่ยน Breathe & Focus เป็นเกมผึ้งเก็บดอกไม้แล้ว\n\n" +
            "• เป่า = ผึ้งลอยขึ้น / หยุดเป่า = ผึ้งลดระดับ\n" +
            "• ดอกชมพู 10 · ดอกเหลือง 20 · โคลเวอร์ 50 คะแนน\n" +
            "• เล่น 60 วินาที (แก้ได้ที่ BFGameManager > Game Duration)\n\n" +
            "กด Play เพื่อทดสอบ (ใช้ปุ่มลูกศรขึ้นแทนการเป่าได้)", "OK");
    }

    public static void EnsureArt()
    {
        BodySprite = BreezBeeArt.Save("bee_body", BreezBeeArt.BeeBody(256), 256, BodyPpu, new Vector2(0.5f, 0.5f));
        WingSprite = BreezBeeArt.Save("bee_wing", BreezBeeArt.BeeWing(128), 128, WingPpu, new Vector2(0.5f, 0.05f));
        PetalsSprite = BreezBeeArt.Save("flower_petals", BreezBeeArt.FlowerPetals(256), 256, 256f, new Vector2(0.5f, 0.5f));
        CenterSprite = BreezBeeArt.Save("flower_center", BreezBeeArt.FlowerCenter(96), 96, 256f, new Vector2(0.5f, 0.5f));
    }

    static string Convert()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var gm = Object.FindObjectOfType<BFGameManager>();
        var player = Object.FindObjectOfType<PlayerController>();
        if (gm == null) return "ไม่พบ BFGameManager ในฉาก Breathe & Focus";
        if (player == null) return "ไม่พบ PlayerController ในฉาก Breathe & Focus";
        Undo.RecordObject(gm, "Bee");
        Undo.RecordObject(player, "Bee");

        // ---------- ปิดสิ่งกีดขวาง ----------
        foreach (var m in Object.FindObjectsOfType<ObstacleSetManager>(true))
        {
            Undo.RecordObject(m.gameObject, "Bee");
            m.gameObject.SetActive(false);
        }
        foreach (var pc in Object.FindObjectsOfType<PlayerCollision>(true)) { Undo.RecordObject(pc, "Bee"); pc.enabled = false; }
        player.clampToTop = true;
        player.clampToBottom = true;
        // ตอบสนองไวขึ้น: เริ่มลอยที่แรงเป่า 25 หยุดที่ 15, เปลี่ยนทิศทันที
        player.breathThreshold = 25;
        player.releaseThreshold = 15;
        player.instantControl = true;
        player.responsiveness = Mathf.Max(player.responsiveness, 80f);

        // ---------- ผึ้ง ----------
        var tr = player.transform;
        float worldW = 1.1f;
        int order = 10;
        string layer = "Default";
        var oldBee = tr.Find("Bee");
        if (oldBee != null)
        {
            var b = oldBee.Find("Body");
            var bs = b != null ? b.GetComponent<SpriteRenderer>() : null;
            if (bs != null) { worldW = bs.bounds.size.x * 0.85f; order = Mathf.Max(0, bs.sortingOrder - 10); layer = bs.sortingLayerName; }
            Undo.DestroyObjectImmediate(oldBee.gameObject);
        }
        else
        {
            var sr = player.GetComponent<SpriteRenderer>();
            if (sr == null) sr = player.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null) { worldW = sr.bounds.size.x * 1.1f; order = sr.sortingOrder; layer = sr.sortingLayerName; }
        }
        // ภาพตัวละครเดิม (แมว): ซ่อน
        foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Undo.RecordObject(sr, "Bee");
            sr.enabled = false;
        }
        var anim = player.GetComponent<Animator>();
        if (anim != null) { Undo.RecordObject(anim, "Bee"); anim.enabled = false; }

        float lossy = Mathf.Max(0.0001f, Mathf.Abs(tr.lossyScale.x));
        float spriteVisibleW = 0.85f * 256f / BodyPpu;                 // ความกว้างผึ้งจริงในภาพ (หน่วย)
        float scale = worldW / lossy / spriteVisibleW;

        var bee = new GameObject("Bee").transform;
        Undo.RegisterCreatedObjectUndo(bee.gameObject, "Bee");
        bee.SetParent(tr, false);
        bee.localScale = Vector3.one * scale;

        // ลำดับการวาด: พื้นหลัง (Canvas order 0) < ดอกไม้ (order+2..+5) < ผึ้ง (order+9..+11)
        var wingBack = Part(bee, "WingBack", WingSprite, new Vector3(-0.16f, 0.17f, 0f), 22f, 1.0f, order + 9, layer, new Color(1f, 1f, 1f, 0.8f));
        var body = Part(bee, "Body", BodySprite, Vector3.zero, 0f, 1f, order + 10, layer, Color.white);
        var wingFront = Part(bee, "WingFront", WingSprite, new Vector3(-0.02f, 0.19f, 0f), -4f, 1.05f, order + 11, layer, new Color(1f, 1f, 1f, 0.95f));
        wingBack.SetParent(body, true); wingFront.SetParent(body, true);

        var visual = player.GetComponent<BeeVisual>();
        if (visual == null) visual = Undo.AddComponent<BeeVisual>(player.gameObject);
        visual.body = body; visual.wingFront = wingFront; visual.wingBack = wingBack;

        // collider วงกลมพอดีตัวผึ้ง
        foreach (var c in player.GetComponents<Collider2D>()) Undo.DestroyObjectImmediate(c);
        var circle = Undo.AddComponent<CircleCollider2D>(player.gameObject);
        circle.radius = worldW * 0.4f / lossy;
        circle.offset = Vector2.zero;
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) { Undo.RecordObject(rb, "Bee"); rb.freezeRotation = true; rb.interpolation = RigidbodyInterpolation2D.Interpolate; }

        // ---------- ดอกไม้ ----------
        var sp = Object.FindObjectOfType<FlowerSpawner>(true);
        if (sp == null)
        {
            var go = new GameObject("FlowerSpawner");
            Undo.RegisterCreatedObjectUndo(go, "Bee");
            sp = go.AddComponent<FlowerSpawner>();
        }
        Undo.RecordObject(sp, "Bee");
        sp.gameObject.SetActive(true);
        sp.petalsSprite = PetalsSprite;
        sp.centerSprite = CenterSprite;
        sp.glowSprite = BreezStatusElementsBuilder.MakeSprite("ui_glow", BreezSpriteArt.Glow(128), 128, 0);
        sp.petalBitSprite = BreezStatusElementsBuilder.MakeSprite("ui_petal", BreezSpriteArt.Petal(64), 64, 0);
        BreezThaiFontInstaller.EnsureFonts();
        sp.font = BreezThaiFontInstaller.BoldFont != null ? BreezThaiFontInstaller.BoldFont : BreezThaiFontInstaller.EnsureFonts();
        sp.flowerSize = Mathf.Clamp(worldW * 1.2f, 1.0f, 2.0f);   // ดอกไม้ใหญ่ขึ้น
        sp.edgeMargin = sp.flowerSize * 0.75f;
        sp.maxStep = Mathf.Max(sp.maxStep, sp.flowerSize * 1.4f);
        sp.sortingOrder = order + 2;
        sp.types = TypesWithSprites();   // ภาพดอกไม้ 3 แบบ (Assets/element/B&F/Flowers)
        foreach (var r in sp.GetComponentsInChildren<Transform>(true)) if (r != sp.transform) Undo.DestroyObjectImmediate(r.gameObject);

        gm.spawner = sp;
        if (gm.gameDuration <= 0f) gm.gameDuration = 60f;

        EditorUtility.SetDirty(gm); EditorUtility.SetDirty(player); EditorUtility.SetDirty(sp);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return null;
    }

    static Transform Part(Transform parent, string name, Sprite sprite, Vector3 pos, float rot, float scale, int order, string layer, Color color)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Bee");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, 0, rot);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        sr.sortingLayerName = layer;
        return go.transform;
    }
}
