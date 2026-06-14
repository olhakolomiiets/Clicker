using System;
using System.Linq;
using PlanetBuilder.Messages;
using PlanetBuilder.Messages.Characters;
using PlanetBuilder.Messages.Dialogs;
using PlanetBuilder.Messages.Tutorial;
using PlanetBuilder.Messages.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using MessageTutorialManager = PlanetBuilder.Messages.Tutorial.TutorialManager;

public static class MetaPlanetTutorialSceneIntegrator
{
    private const string ScenePath = "Assets/_Scenes/PlanetMeta.unity";
    private const string OverlayPrefabPath = "Assets/_Prefabs/UI/TutorialOverlay.prefab";
    private const string CharacterDatabasePath = "Assets/_Data/Tutorial/CharacterDatabase.asset";
    private const string RootName = "MetaPlanetTutorial";
    private const string ViewCanvasName = "TutorialMessageCanvas";

    [MenuItem("Tools/Planet Builder/Integrate Meta Planet Tutorial Scene")]
    public static void Integrate()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        MetaGameRules metaGameRules = FindInScene<MetaGameRules>(scene);
        GameManager gameManager = FindInScene<GameManager>(scene);
        MetaUIController metaUIController = FindInScene<MetaUIController>(scene);
        MetaPlanetManager metaPlanetManager = FindInScene<MetaPlanetManager>(scene);
        UpgradePanelUI upgradePanel = FindInScene<UpgradePanelUI>(scene);
        ScorePanel diamondScorePanel = FindInScene<ScorePanel>(scene);
        MetaVariantsController houseController = FindHouseController(scene);
        MetaUpgradeItemController houseUpgradeController = FindHouseUpgradeController(scene);

        Require(metaGameRules, nameof(MetaGameRules));
        Require(gameManager, nameof(GameManager));
        Require(metaUIController, nameof(MetaUIController));
        Require(metaPlanetManager, nameof(MetaPlanetManager));
        Require(upgradePanel, nameof(UpgradePanelUI));
        Require(diamondScorePanel, "diamond ScorePanel");
        Require(houseController, "house MetaVariantsController");
        Require(houseUpgradeController, "house MetaUpgradeItemController");

        GameObject root = FindRoot(scene, RootName) ?? new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        MessageManager messageManager = EnsureComponent<MessageManager>(root);
        DialogManager dialogManager = EnsureComponent<DialogManager>(root);
        TutorialInteractionOverlay overlay = EnsureOverlay(root.transform);
        TutorialHighlightSystem highlightSystem = overlay.GetComponent<TutorialHighlightSystem>();
        TutorialView tutorialView = EnsureTutorialView(root.transform);
        MessageView dialogView = EnsureDialogView(root.transform, out Button nextButton);
        ToastView toastView = EnsureToastView(root.transform);
        MetaTutorialModalView rewardView = EnsureModalView(root.transform, "RewardView");
        MetaTutorialModalView contractView = EnsureModalView(root.transform, "ContractView");
        MetaTutorialVideoView videoView = EnsureVideoView(root.transform);
        MessageTutorialManager tutorialManager = EnsureComponent<MessageTutorialManager>(root);
        MetaPlanetTutorialController tutorialController = EnsureComponent<MetaPlanetTutorialController>(root);

        SetReference(tutorialManager, "_interactionOverlay", overlay);
        SetReference(tutorialManager, "_highlightSystem", highlightSystem);
        SetBool(tutorialManager, "_startAutomatically", false);

        SetReference(messageManager, "_tutorialView", tutorialView);
        SetReference(messageManager, "_messageView", dialogView);
        SetReference(messageManager, "_toastView", toastView);
        SetReference(dialogManager, "_nextButton", nextButton);

        SetReference(tutorialController, "_tutorialManager", tutorialManager);
        SetReference(tutorialController, "_dialogManager", dialogManager);
        SetReference(tutorialController, "_interactionOverlay", overlay);
        SetReference(tutorialController, "_metaGameRules", metaGameRules);
        SetReference(tutorialController, "_gameManager", gameManager);
        SetReference(tutorialController, "_metaUIController", metaUIController);
        SetReference(tutorialController, "_metaPlanetManager", metaPlanetManager);
        SetReference(tutorialController, "_houseController", houseController);
        SetReference(tutorialController, "_houseUpgradeController", houseUpgradeController);
        SetReference(tutorialController, "_upgradePanel", upgradePanel);
        SetReference(tutorialController, "_rewardView", rewardView);
        SetReference(tutorialController, "_contractView", contractView);
        SetReference(tutorialController, "_videoView", videoView);
        SetReference(tutorialController, "_diamondBalanceTarget", diamondScorePanel.gameObject);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Meta Planet tutorial scene integration complete.");
    }

    private static TutorialInteractionOverlay EnsureOverlay(Transform parent)
    {
        TutorialInteractionOverlay existing = parent.GetComponentInChildren<TutorialInteractionOverlay>(true);

        if (existing != null)
            return existing;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPrefabPath);
        Require(prefab, "TutorialOverlay prefab");

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "TutorialOverlay";
        instance.transform.SetParent(parent, false);
        return instance.GetComponent<TutorialInteractionOverlay>();
    }

    private static TutorialView EnsureTutorialView(Transform parent)
    {
        TutorialView existing = parent.GetComponentInChildren<TutorialView>(true);

        if (existing != null)
        {
            ConfigureTutorialCanvas(existing.GetComponentInParent<Canvas>());
            return existing;
        }

        GameObject canvasObject = new(
            ViewCanvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        ConfigureTutorialCanvas(canvas);

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);

        GameObject panel = CreateUIObject("TutorialView", canvasObject.transform, typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.05f, 0.05f);
        panelRect.anchorMax = new Vector2(0.95f, 0.25f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.94f);

        GameObject portraitObject = CreateUIObject("Portrait", panel.transform, typeof(Image));
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0.02f, 0.12f);
        portraitRect.anchorMax = new Vector2(0.22f, 0.88f);
        portraitRect.offsetMin = Vector2.zero;
        portraitRect.offsetMax = Vector2.zero;
        Image portraitImage = portraitObject.GetComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;

        TMP_Text nameText = CreateText(
            "CharacterName",
            panel.transform,
            new Vector2(0.25f, 0.7f),
            new Vector2(0.95f, 0.92f),
            32f);
        TMP_Text messageText = CreateText(
            "MessageText",
            panel.transform,
            new Vector2(0.25f, 0.12f),
            new Vector2(0.95f, 0.68f),
            30f);

        CharacterPortraitController portraitController = panel.AddComponent<CharacterPortraitController>();
        SetReference(
            portraitController,
            "_characterDatabase",
            AssetDatabase.LoadAssetAtPath<CharacterDatabase>(CharacterDatabasePath));
        SetReference(portraitController, "_portraitImage", portraitImage);
        SetReference(portraitController, "_characterNameText", nameText);

        TutorialView tutorialView = panel.AddComponent<TutorialView>();
        SetReference(tutorialView, "_messageText", messageText);
        SetReference(tutorialView, "_portraitController", portraitController);
        panel.SetActive(false);
        return tutorialView;
    }

    private static MessageView EnsureDialogView(Transform parent, out Button nextButton)
    {
        DialogView existing = parent.GetComponentInChildren<DialogView>(true);

        if (existing != null)
        {
            nextButton = existing.GetComponentInChildren<Button>(true);
            Require(nextButton, "DialogView Next button");
            return existing;
        }

        GameObject canvasObject = new(
            "DialogMessageCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        ConfigureMessageCanvas(canvas, 32762);

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);

        GameObject panel = CreateUIObject("DialogView", canvasObject.transform, typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.05f, 0.05f);
        panelRect.anchorMax = new Vector2(0.95f, 0.25f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.94f);

        GameObject portraitObject = CreateUIObject("Portrait", panel.transform, typeof(Image));
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0.02f, 0.12f);
        portraitRect.anchorMax = new Vector2(0.22f, 0.88f);
        portraitRect.offsetMin = Vector2.zero;
        portraitRect.offsetMax = Vector2.zero;
        Image portraitImage = portraitObject.GetComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;

        TMP_Text nameText = CreateText(
            "CharacterName",
            panel.transform,
            new Vector2(0.25f, 0.7f),
            new Vector2(0.78f, 0.92f),
            32f);
        TMP_Text messageText = CreateText(
            "MessageText",
            panel.transform,
            new Vector2(0.25f, 0.12f),
            new Vector2(0.78f, 0.68f),
            30f);

        GameObject nextButtonObject = CreateUIObject("NextButton", panel.transform, typeof(Image), typeof(Button));
        RectTransform nextRect = nextButtonObject.GetComponent<RectTransform>();
        nextRect.anchorMin = new Vector2(0.8f, 0.2f);
        nextRect.anchorMax = new Vector2(0.96f, 0.8f);
        nextRect.offsetMin = Vector2.zero;
        nextRect.offsetMax = Vector2.zero;
        nextButtonObject.GetComponent<Image>().color = new Color(0.15f, 0.35f, 0.65f, 1f);
        nextButton = nextButtonObject.GetComponent<Button>();

        TMP_Text buttonText = CreateText(
            "Text",
            nextButtonObject.transform,
            Vector2.zero,
            Vector2.one,
            28f);
        buttonText.text = "Next";
        buttonText.alignment = TextAlignmentOptions.Center;

        CharacterPortraitController portraitController = panel.AddComponent<CharacterPortraitController>();
        SetReference(
            portraitController,
            "_characterDatabase",
            AssetDatabase.LoadAssetAtPath<CharacterDatabase>(CharacterDatabasePath));
        SetReference(portraitController, "_portraitImage", portraitImage);
        SetReference(portraitController, "_characterNameText", nameText);

        DialogView dialogView = panel.AddComponent<DialogView>();
        SetReference(dialogView, "_messageText", messageText);
        SetReference(dialogView, "_portraitController", portraitController);
        panel.SetActive(false);
        return dialogView;
    }

    private static ToastView EnsureToastView(Transform parent)
    {
        ToastView existing = parent.GetComponentInChildren<ToastView>(true);

        if (existing != null)
            return existing;

        GameObject canvasObject = CreateCanvas("ToastCanvas", parent, 32763);
        GameObject panel = CreateUIObject("ToastView", canvasObject.transform, typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.18f, 0.78f);
        panelRect.anchorMax = new Vector2(0.82f, 0.88f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.94f);

        TMP_Text messageText = CreateText("MessageText", panel.transform, new Vector2(0.06f, 0.1f), new Vector2(0.94f, 0.9f), 30f);
        messageText.alignment = TextAlignmentOptions.Center;

        ToastView toastView = panel.AddComponent<ToastView>();
        SetReference(toastView, "_messageText", messageText);
        panel.SetActive(false);
        return toastView;
    }

    private static MetaTutorialModalView EnsureModalView(Transform parent, string name)
    {
        MetaTutorialModalView[] existingViews = parent.GetComponentsInChildren<MetaTutorialModalView>(true);
        MetaTutorialModalView existing = existingViews.FirstOrDefault(view => view.name == name);

        if (existing != null)
            return existing;

        GameObject canvasObject = FindChild(parent, "MetaTutorialModalCanvas") ?? CreateCanvas("MetaTutorialModalCanvas", parent, 32764);
        GameObject panel = CreateUIObject(name, canvasObject.transform, typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.08f, 0.32f);
        panelRect.anchorMax = new Vector2(0.92f, 0.68f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.98f);

        TMP_Text titleText = CreateText("Title", panel.transform, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.9f), 40f);
        titleText.alignment = TextAlignmentOptions.Center;
        TMP_Text descriptionText = CreateText("Description", panel.transform, new Vector2(0.08f, 0.4f), new Vector2(0.92f, 0.68f), 30f);
        descriptionText.alignment = TextAlignmentOptions.Center;
        TMP_Text rewardText = CreateText("Reward", panel.transform, new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.38f), 34f);
        rewardText.alignment = TextAlignmentOptions.Center;

        GameObject buttonObject = CreateUIObject("ConfirmButton", panel.transform, typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.28f, 0.06f);
        buttonRect.anchorMax = new Vector2(0.72f, 0.22f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        buttonObject.GetComponent<Image>().color = new Color(0.15f, 0.35f, 0.65f, 1f);

        TMP_Text buttonText = CreateText("Text", buttonObject.transform, Vector2.zero, Vector2.one, 28f);
        buttonText.alignment = TextAlignmentOptions.Center;

        MetaTutorialModalView view = panel.AddComponent<MetaTutorialModalView>();
        SetReference(view, "_titleText", titleText);
        SetReference(view, "_descriptionText", descriptionText);
        SetReference(view, "_rewardText", rewardText);
        SetReference(view, "_buttonText", buttonText);
        SetReference(view, "_button", buttonObject.GetComponent<Button>());
        panel.SetActive(false);
        return view;
    }

    private static MetaTutorialVideoView EnsureVideoView(Transform parent)
    {
        MetaTutorialVideoView existing = parent.GetComponentInChildren<MetaTutorialVideoView>(true);

        if (existing != null)
            return existing;

        GameObject canvasObject = CreateCanvas("TutorialVideoCanvas", parent, 32765);
        GameObject panel = CreateUIObject("TutorialVideoView", canvasObject.transform, typeof(Image), typeof(VideoPlayer));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = Color.black;

        VideoPlayer videoPlayer = panel.GetComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;

        GameObject buttonObject = CreateUIObject("SkipButton", panel.transform, typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.64f, 0.04f);
        buttonRect.anchorMax = new Vector2(0.94f, 0.12f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        buttonObject.GetComponent<Image>().color = new Color(0.15f, 0.35f, 0.65f, 1f);

        TMP_Text buttonText = CreateText("Text", buttonObject.transform, Vector2.zero, Vector2.one, 28f);
        buttonText.alignment = TextAlignmentOptions.Center;

        MetaTutorialVideoView view = panel.AddComponent<MetaTutorialVideoView>();
        SetReference(view, "_videoPlayer", videoPlayer);
        SetReference(view, "_skipButton", buttonObject.GetComponent<Button>());
        SetReference(view, "_skipButtonText", buttonText);
        panel.SetActive(false);
        return view;
    }

    private static GameObject CreateCanvas(string name, Transform parent, int sortingOrder)
    {
        GameObject canvasObject = new(
            name,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        ConfigureMessageCanvas(canvas, sortingOrder);

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        return canvasObject;
    }

    private static void ConfigureTutorialCanvas(Canvas canvas)
    {
        Require(canvas, nameof(Canvas));

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.localScale = Vector3.one;

        ConfigureMessageCanvas(canvas, 32761);
    }

    private static void ConfigureMessageCanvas(Canvas canvas, int sortingOrder)
    {
        Require(canvas, nameof(Canvas));
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize)
    {
        GameObject textObject = CreateUIObject(name, parent, typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateUIObject(string name, Transform parent, params Type[] components)
    {
        Type[] types = new Type[components.Length + 2];
        types[0] = typeof(RectTransform);
        types[1] = typeof(CanvasRenderer);
        Array.Copy(components, 0, types, 2, components.Length);

        GameObject gameObject = new(name, types);
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static MetaVariantsController FindHouseController(Scene scene)
    {
        return FindAllInScene<MetaVariantsController>(scene)
            .FirstOrDefault(controller =>
                ContainsHouse(controller.controllerId) ||
                controller.items != null &&
                controller.items.Any(item =>
                    item != null &&
                    item.itemData != null &&
                    ContainsHouse(item.itemData.ItemName)));
    }

    private static MetaUpgradeItemController FindHouseUpgradeController(Scene scene)
    {
        return FindAllInScene<MetaUpgradeItemController>(scene)
            .FirstOrDefault(controller =>
                controller.data != null &&
                ContainsHouse(controller.data.ItemName));
    }

    private static bool ContainsHouse(string value)
    {
        return !string.IsNullOrEmpty(value) &&
               value.IndexOf("house", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
    }

    private static GameObject FindChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        return child != null ? child.gameObject : null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        return FindAllInScene<T>(scene).FirstOrDefault();
    }

    private static T[] FindAllInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }

    private static void SetReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
    {
        Require(value, $"{target.name}.{fieldName}");
        SerializedObject serializedObject = new(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);

        if (property == null)
            throw new InvalidOperationException($"Serialized field '{fieldName}' was not found on {target.GetType().Name}.");

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetBool(UnityEngine.Object target, string fieldName, bool value)
    {
        SerializedObject serializedObject = new(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);

        if (property == null)
            throw new InvalidOperationException($"Serialized field '{fieldName}' was not found on {target.GetType().Name}.");

        property.boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void Require(UnityEngine.Object value, string dependency)
    {
        if (value == null)
            throw new InvalidOperationException($"Missing required dependency: {dependency}.");
    }
}
