using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// Breez > Restyle Gadget Setting   (กดซ้ำได้)
//   การ์ดใหม่: ตัวอย่างไอคอน (สีที่เลือก) + ชื่อ + สถานะ | ช่องชื่อ · ไอคอน 6 แบบ · สี 8 สี · ปุ่มบันทึก/ยกเลิก
//   ของเดิม (Panel) ถูกซ่อนไว้ ไม่ลบ
// =====================================================================
public static class BreezGadgetSettingRestyle
{
    const string ScenePath = "Assets/Scenes/GadgetSetting.unity";

    static readonly Color Ink = new Color32(0x2E, 0x3B, 0x35, 255);
    static readonly Color Muted = new Color32(0x8A, 0x94, 0x8B, 255);
    static readonly Color Field = new Color32(0xF4, 0xF6, 0xEE, 255);
    static readonly Color Green = new Color32(0xA9, 0xC7, 0x7E, 255);
    static readonly Color Soft = new Color32(0xEC, 0xEF, 0xE6, 255);

    /// <summary>สีให้เลือก (พาสเทลเข้ากับเกม + เทาเข้มแบบเดิม)</summary>
    public static readonly string[] Palette = { "#555555", "#A9C77E", "#7CCFB0", "#8EC5F0", "#B9A6E0", "#F2A7B8", "#F5B48A", "#EDC965" };

    static TMP_FontAsset regular, bold;
    static Sprite card, circle, pill, ring;

    [MenuItem("Breez/Restyle Gadget Setting")]
    public static void Run()
    {
        if (!File.Exists(ScenePath)) { EditorUtility.DisplayDialog("Breez", "ไม่พบ " + ScenePath, "OK"); return; }
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        var scene = EditorSceneManager.GetActiveScene();
        var screen = Object.FindObjectOfType<GadgetSettingsScreen>(true);
        if (screen == null) { EditorUtility.DisplayDialog("Breez", "ไม่พบ GadgetSettingsScreen (กด Breez > Create Gadget Setting Scene ก่อน)", "OK"); return; }
        var C = screen.transform;
        Undo.RecordObject(screen, "Restyle Gadget Setting");

        regular = BreezThaiFontInstaller.EnsureFonts();
        bold = BreezThaiFontInstaller.BoldFont;
        if (bold == null) bold = regular;
        card = BreezStatusElementsBuilder.MakeSprite("ui_card", BreezSpriteArt.RoundedRect(128, 40), 128, 40);
        circle = BreezStatusElementsBuilder.MakeSprite("ui_circle", BreezSpriteArt.Circle(256), 256, 0);
        pill = BreezStatusElementsBuilder.MakeSprite("ui_pill", BreezSpriteArt.RoundedRect(128, 63), 128, 63);
        ring = BreezStatusElementsBuilder.MakeSprite("ui_ring_thin", BreezSpriteArt.Ring(256, 0.1f), 256, 0);

        // ของเดิม: ซ่อน
        var oldPanel = C.Find("Panel");
        if (oldPanel != null) { Undo.RecordObject(oldPanel.gameObject, "hide"); oldPanel.gameObject.SetActive(false); }
        var old = C.Find("SettingCard");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // ---------- การ์ด ----------
        var cardImg = Img(C, "SettingCard", card, Color.white, 0, -30, 1400, 800);
        cardImg.type = Image.Type.Sliced;
        Shadow(cardImg.gameObject, 0.07f, 8);
        var K = cardImg.transform;

        Left(Text(K, "Title", "ตั้งค่า Breath Gadget", bold, 52, Ink, -640, 330, 900, 70), -640);
        Left(Text(K, "Subtitle", "ตั้งชื่อ เลือกไอคอน และสีให้เป็นของคุณ แล้วกดบันทึก", regular, 27, Muted, -640, 276, 900, 44), -640);

        // ---------- ซ้าย: ตัวอย่าง ----------
        var previewBg = Img(K, "PreviewBg", circle, new Color32(0xEE, 0xEE, 0xEE, 255), -400, 30, 320, 320);
        var halo = Img(K, "PreviewRing", ring, new Color(1f, 1f, 1f, 0.9f), -400, 30, 344, 344);
        halo.transform.SetSiblingIndex(previewBg.transform.GetSiblingIndex());
        var icons = new System.Collections.Generic.Dictionary<string, Sprite>();
        foreach (var id in BreezSpriteArt.GadgetIcons)
            icons[id] = BreezStatusElementsBuilder.MakeSprite("icon_" + id, BreezSpriteArt.GadgetIcon(id, 192), 192, 0);
        var previewIcon = Img(K, "PreviewIcon", icons["cat"], new Color32(85, 85, 85, 255), -400, 30, 190, 190);
        previewIcon.preserveAspect = true;
        var previewName = Text(K, "PreviewName", "Breath Gadget", bold, 40, Ink, -400, -175, 520, 56);

        var statusPill = Img(K, "StatusPill", pill, Soft, -400, -240, 300, 56);
        statusPill.type = Image.Type.Sliced;
        var dot = Img(statusPill.transform, "StatusDot", circle, Green, -112, 0, 18, 18);
        var status = Text(statusPill.transform, "Status", "ยังไม่เชื่อมต่อ", regular, 26, Ink, 12, 0, 230, 50);
        var battery = Text(K, "Battery", "", regular, 24, Muted, -400, -292, 400, 36);

        // เส้นคั่น
        Img(K, "Divider", null, new Color32(0xEC, 0xEE, 0xE6, 255), -130, 0, 2, 560);

        // ---------- ขวา: ฟอร์ม ----------
        const float X0 = -90f;
        Left(Text(K, "NameLabel", "ชื่ออุปกรณ์", bold, 30, Ink, X0, 205, 400, 44), X0);
        var fieldBg = Img(K, "NameField", pill, Field, X0 + 360, 145, 720, 82);
        fieldBg.type = Image.Type.Sliced;
        var input = NameInput(fieldBg.transform, 690, 82);

        Left(Text(K, "IconLabel", "ไอคอน", bold, 30, Ink, X0, 62, 400, 44), X0);
        var iconOpts = new GadgetSettingsScreen.IconOption[BreezSpriteArt.GadgetIcons.Length];
        for (int i = 0; i < iconOpts.Length; i++)
        {
            string id = BreezSpriteArt.GadgetIcons[i];
            var frame = Img(K, "Icon " + id, circle, new Color32(240, 242, 234, 255), X0 + 50 + i * 118, -4, 100, 100);
            frame.raycastTarget = true;
            var b = frame.gameObject.AddComponent<Button>();
            b.targetGraphic = frame;
            var ic = Img(frame.transform, "Icon", icons[id], new Color32(120, 120, 120, 255), 0, 0, 62, 62);
            ic.preserveAspect = true;
            iconOpts[i] = new GadgetSettingsScreen.IconOption { id = id, button = b, frame = frame, sprite = icons[id], icon = ic };
        }

        Left(Text(K, "ColorLabel", "สีไอคอน", bold, 30, Ink, X0, -92, 400, 44), X0);
        var colorOpts = new GadgetSettingsScreen.ColorOption[Palette.Length];
        for (int i = 0; i < Palette.Length; i++)
        {
            ColorUtility.TryParseHtmlString(Palette[i], out var col);
            float x = X0 + 38 + i * 90;
            var hit = Img(K, "Color " + Palette[i], null, new Color(1, 1, 1, 0), x, -152, 80, 80);
            hit.raycastTarget = true;
            var b = hit.gameObject.AddComponent<Button>();
            var rg = Img(hit.transform, "Ring", ring, col, 0, 0, 80, 80);
            var sw = Img(hit.transform, "Swatch", circle, col, 0, 0, 58, 58);
            b.targetGraphic = sw;
            Shadow(sw.gameObject, 0.10f, 3);
            colorOpts[i] = new GadgetSettingsScreen.ColorOption { hex = Palette[i], button = b, swatch = sw, ring = rg };
        }

        // ปุ่ม
        var reset = PillButton(K, "ResetButton", Soft, Ink, "ยกเลิก", 330, -282, 210, 84);
        var save = PillButton(K, "SaveButton", Green, Color.white, "บันทึก", 560, -282, 230, 84);
        var saveLabel = save.GetComponentInChildren<TMP_Text>();
        var cb = save.colors; cb.disabledColor = new Color(1f, 1f, 1f, 0.55f); save.colors = cb;
        var toast = Left(Text(K, "Toast", "", regular, 24, Muted, X0, -360, 900, 36), X0);

        // ---------- ต่อสาย ----------
        screen.iconImage = previewIcon;
        screen.iconBackground = previewBg;
        screen.iconButton = null;
        screen.iconPicker = null;
        screen.iconOptions = iconOpts;
        screen.colorOptions = colorOpts;
        screen.nameInput = input;
        screen.previewName = previewName;
        screen.statusText = status;
        screen.statusDot = dot;
        screen.batteryText = battery;
        screen.toastText = toast;
        screen.saveButton = save;
        screen.saveLabel = saveLabel;
        screen.resetButton = reset;
        if (screen.backButton == null)
        {
            var bb = C.Find("BackButton");
            if (bb != null) screen.backButton = bb.GetComponent<Button>();
        }
        if (screen.backButton != null) screen.backButton.transform.SetAsLastSibling();

        BreezThaiFontInstaller.Apply(cardImg.gameObject);
        EditorUtility.SetDirty(screen);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Breez", "จัดหน้า Gadget Setting ใหม่แล้ว\n\n• แก้ชื่อ / ไอคอน / สีไอคอน\n• กด \"บันทึก\" เพื่อบันทึก (ปุ่มกลับจะเตือนถ้ายังไม่บันทึก)\n\nกด Play เพื่อทดสอบ", "OK");
    }

    // ---------------------------------------------------------------
    static TMP_InputField NameInput(Transform parent, float w, float h)
    {
        var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = "NameInput";
        Undo.RegisterCreatedObjectUndo(go, "NameInput");
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        Place(rt, 0, 0, w, h);
        go.GetComponent<Image>().color = new Color(1, 1, 1, 0);
        var field = go.GetComponent<TMP_InputField>();
        field.pointSize = 36;
        var area = field.textViewport;
        if (area != null) { area.offsetMin = new Vector2(24, 0); area.offsetMax = new Vector2(-24, 0); }
        field.textComponent.fontSize = 36;
        field.textComponent.color = Ink;
        field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        if (regular != null) field.textComponent.font = regular;
        if (field.placeholder is TMP_Text ph)
        {
            ph.text = "ตั้งชื่ออุปกรณ์";
            ph.fontSize = 36;
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            ph.color = new Color(Muted.r, Muted.g, Muted.b, 0.6f);
            if (regular != null) ph.font = regular;
        }
        field.caretColor = Green;
        field.selectionColor = new Color(Green.r, Green.g, Green.b, 0.35f);
        field.text = "Breath Gadget";
        return field;
    }

    static Button PillButton(Transform parent, string name, Color bg, Color fg, string label, float x, float y, float w, float h)
    {
        var img = Img(parent, name, pill, bg, x, y, w, h);
        img.type = Image.Type.Sliced;
        img.raycastTarget = true;
        Shadow(img.gameObject, 0.08f, 5);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors; cb.highlightedColor = new Color(0.96f, 0.96f, 0.96f); cb.pressedColor = new Color(0.86f, 0.86f, 0.86f); b.colors = cb;
        Text(img.transform, "Label", label, bold, 32, fg, 0, 0, w, h);
        return b;
    }

    static Image Img(Transform parent, string name, Sprite sprite, Color color, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        Place((RectTransform)go.transform, x, y, w, h);
        return img;
    }

    static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, x, y, w, h);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    /// <summary>ชิดซ้าย: x = ขอบซ้าย</summary>
    static TextMeshProUGUI Left(TextMeshProUGUI t, float left)
    {
        var rt = t.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(left, rt.anchoredPosition.y);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        return t;
    }

    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    static void Shadow(GameObject go, float alpha, float dist)
    {
        var s = go.AddComponent<Shadow>();
        s.effectColor = new Color(0, 0, 0, alpha);
        s.effectDistance = new Vector2(0, -dist);
    }
}
