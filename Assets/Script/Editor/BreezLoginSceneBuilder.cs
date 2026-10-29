using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================
// เมนู Breez > Create Login Scene
// สร้าง Assets/Scenes/Login.unity พร้อม UI + LoginUI ที่ต่อสายครบ
// และตั้งให้เป็น scene แรกใน Build Settings
// ปรับหน้าตาต่อใน Unity ได้ตามสบาย (ห้ามลบ component LoginUI)
// =====================================================================
public static class BreezLoginSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Login.unity";

    static readonly Color Background = new Color32(247, 248, 244, 255);
    static readonly Color Card = Color.white;
    static readonly Color Ink = new Color32(41, 53, 45, 255);
    static readonly Color Muted = new Color32(130, 145, 127, 255);
    static readonly Color Field = new Color32(242, 245, 239, 255);
    static readonly Color Green = new Color32(107, 152, 97, 255);
    static readonly Color SoftGreen = new Color32(237, 244, 231, 255);

    [MenuItem("Breez/Create Login Scene")]
    public static void CreateLoginScene()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Breez", "Login.unity already exists. Replace it?", "Replace", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Background;
        }

        // ---------- Canvas ----------
        var canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var bg = NewImage("Background", canvasGO.transform, Background);
        Stretch(bg.rectTransform);

        // ---------- Card ----------
        var card = NewImage("LoginCard", canvasGO.transform, Card);
        var rt = card.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(640, 0);
        var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(56, 56, 52, 44);
        layout.spacing = 16;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        NewText("Brand", card.transform, "breez", 44, Ink, FontStyles.Bold, 56);
        var title = NewText("Title", card.transform, "Welcome back", 34, Ink, FontStyles.Normal, 48);
        NewText("Subtitle", card.transform, "Sign in to save your breathing progress", 22, Muted, FontStyles.Normal, 34);
        Spacer(card.transform, 6);

        // name (เฉพาะตอนสมัคร)
        var nameGroup = new GameObject("NameGroup", typeof(RectTransform));
        nameGroup.transform.SetParent(card.transform, false);
        var ng = nameGroup.AddComponent<VerticalLayoutGroup>();
        ng.spacing = 8; ng.childControlWidth = true; ng.childControlHeight = true;
        ng.childForceExpandWidth = true; ng.childForceExpandHeight = false;
        NewText("NameLabel", nameGroup.transform, "Your name", 22, Muted, FontStyles.Normal, 30);
        var nameInput = NewInput("NameInput", nameGroup.transform, "e.g. Pim", TMP_InputField.ContentType.Standard);

        NewText("EmailLabel", card.transform, "Email", 22, Muted, FontStyles.Normal, 30);
        var emailInput = NewInput("EmailInput", card.transform, "you@example.com", TMP_InputField.ContentType.EmailAddress);
        NewText("PasswordLabel", card.transform, "Password", 22, Muted, FontStyles.Normal, 30);
        var passwordInput = NewInput("PasswordInput", card.transform, "At least 6 characters", TMP_InputField.ContentType.Password);

        var status = NewText("Status", card.transform, "", 20, Green, FontStyles.Normal, 30);

        var submit = NewButton("SubmitButton", card.transform, "Sign in", Green, Color.white, 70, out var submitLabel);
        var switchMode = NewButton("SwitchModeButton", card.transform, "New here? Create account", SoftGreen, Green, 56, out var switchLabel);

        // ---------- ต่อสาย LoginUI ----------
        var ui = canvasGO.AddComponent<LoginUI>();
        ui.nameInput = nameInput;
        ui.nameGroup = nameGroup;
        ui.emailInput = emailInput;
        ui.passwordInput = passwordInput;
        ui.submitButton = submit;
        ui.submitLabel = submitLabel;
        ui.switchModeButton = switchMode;
        ui.switchModeLabel = switchLabel;
        ui.titleText = title;
        ui.statusText = status;
        ui.nextScene = BreezConfig.HomeScene;
        nameGroup.SetActive(false);

        foreach (var sceneRoot in scene.GetRootGameObjects()) BreezThaiFontInstaller.Apply(sceneRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettingsFirst(ScenePath);

        EditorUtility.DisplayDialog("Breez",
            "Created Assets/Scenes/Login.unity and set it as the first scene in Build Settings.\n\n" +
            "Press Play to test sign up / sign in.", "OK");
    }

    [MenuItem("Breez/Add Account Actions To Open Scene")]
    public static void AddAccountActions()
    {
        var existing = Object.FindObjectOfType<AccountActions>();
        if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
        var go = new GameObject("[Breez Account]");
        go.AddComponent<AccountActions>();
        Undo.RegisterCreatedObjectUndo(go, "Add Account Actions");
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    [MenuItem("Breez/Sign Out (clear saved login)")]
    public static void ClearSavedLogin()
    {
        PlayerPrefs.DeleteKey("breez.session");
        PlayerPrefs.Save();
        Debug.Log("[Breez] Saved login cleared.");
    }

    // ---------------------------------------------------------------
    static void AddToBuildSettingsFirst(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == path);
        list.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Spacer(Transform parent, float height)
    {
        var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, FontStyles style, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.enableWordWrapping = true;
        go.AddComponent<LayoutElement>().preferredHeight = height;
        return t;
    }

    static TMP_InputField NewInput(string name, Transform parent, string placeholder, TMP_InputField.ContentType type)
    {
        var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = Field;
        go.AddComponent<LayoutElement>().preferredHeight = 64;

        var field = go.GetComponent<TMP_InputField>();
        field.contentType = type;
        field.pointSize = 24;

        var textArea = field.textViewport;
        if (textArea != null)
        {
            textArea.offsetMin = new Vector2(18, 6);
            textArea.offsetMax = new Vector2(-18, -6);
        }

        field.textComponent.fontSize = 24;
        field.textComponent.color = Ink;
        field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;

        if (field.placeholder is TMP_Text ph)
        {
            ph.text = placeholder;
            ph.fontSize = 24;
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            ph.color = new Color(Muted.r, Muted.g, Muted.b, 0.6f);
        }
        return field;
    }

    static Button NewButton(string name, Transform parent, string label, Color bg, Color fg, float height, out TMP_Text labelText)
    {
        var go = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;
        go.AddComponent<LayoutElement>().preferredHeight = height;

        labelText = go.GetComponentInChildren<TMP_Text>();
        labelText.text = label;
        labelText.color = fg;
        labelText.fontSize = 26;
        labelText.fontStyle = FontStyles.Bold;
        return go.GetComponent<Button>();
    }
}
