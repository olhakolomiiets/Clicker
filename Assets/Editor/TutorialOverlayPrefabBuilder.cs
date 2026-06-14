using PlanetBuilder.Messages.Tutorial;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class TutorialOverlayPrefabBuilder
{
    private const string PrefabPath = "Assets/_Prefabs/UI/TutorialOverlay.prefab";
    private const int SortingOrder = 32760;

    [MenuItem("Tools/Planet Builder/Create Tutorial Overlay Prefab")]
    public static void CreateTutorialOverlayPrefab()
    {
        EnsureFolder("Assets/_Prefabs");
        EnsureFolder("Assets/_Prefabs/UI");

        GameObject root = new("TutorialOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));

        try
        {
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Image topBlocker = CreateImage("TopBlocker", root.transform, Color.black, false);
            Image bottomBlocker = CreateImage("BottomBlocker", root.transform, Color.black, false);
            Image leftBlocker = CreateImage("LeftBlocker", root.transform, Color.black, false);
            Image rightBlocker = CreateImage("RightBlocker", root.transform, Color.black, false);
            Image highlight = CreateImage("Highlight", root.transform, new Color(1f, 0.85f, 0.15f, 0.35f), false);
            RectTransform arrowRoot = CreateArrow(root.transform, out Image[] arrowImages);
            Image raycastShield = CreateImage("RaycastShield", root.transform, Color.clear, true);

            TutorialInteractionOverlay overlay = root.AddComponent<TutorialInteractionOverlay>();
            SerializedObject serializedOverlay = new(overlay);
            serializedOverlay.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            serializedOverlay.FindProperty("_overlayRoot").objectReferenceValue = root.transform;
            serializedOverlay.FindProperty("_raycastShield").objectReferenceValue = raycastShield;
            serializedOverlay.FindProperty("_topBlocker").objectReferenceValue = topBlocker;
            serializedOverlay.FindProperty("_bottomBlocker").objectReferenceValue = bottomBlocker;
            serializedOverlay.FindProperty("_leftBlocker").objectReferenceValue = leftBlocker;
            serializedOverlay.FindProperty("_rightBlocker").objectReferenceValue = rightBlocker;
            serializedOverlay.FindProperty("_sortingOrder").intValue = SortingOrder;
            serializedOverlay.ApplyModifiedPropertiesWithoutUndo();

            TutorialHighlightSystem highlightSystem = root.AddComponent<TutorialHighlightSystem>();
            SerializedObject serializedHighlight = new(highlightSystem);
            serializedHighlight.FindProperty("_overlayRoot").objectReferenceValue = root.transform;
            serializedHighlight.FindProperty("_highlightImage").objectReferenceValue = highlight;
            serializedHighlight.FindProperty("_arrowRoot").objectReferenceValue = arrowRoot;
            SerializedProperty arrowImagesProperty = serializedHighlight.FindProperty("_arrowImages");
            arrowImagesProperty.arraySize = arrowImages.Length;

            for (int i = 0; i < arrowImages.Length; i++)
                arrowImagesProperty.GetArrayElementAtIndex(i).objectReferenceValue = arrowImages[i];

            serializedHighlight.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created tutorial overlay prefab: {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static RectTransform CreateArrow(Transform parent, out Image[] arrowImages)
    {
        GameObject arrowObject = new("Arrow", typeof(RectTransform));
        arrowObject.transform.SetParent(parent, false);

        RectTransform arrowRoot = arrowObject.GetComponent<RectTransform>();
        arrowRoot.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRoot.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRoot.pivot = new Vector2(0.5f, 0.5f);
        arrowRoot.sizeDelta = new Vector2(72f, 40f);

        Image shaft = CreateImage("Shaft", arrowRoot, Color.white, false);
        RectTransform shaftRect = shaft.rectTransform;
        shaftRect.anchorMin = new Vector2(0f, 0.5f);
        shaftRect.anchorMax = new Vector2(0f, 0.5f);
        shaftRect.pivot = new Vector2(0f, 0.5f);
        shaftRect.anchoredPosition = new Vector2(0f, 0f);
        shaftRect.sizeDelta = new Vector2(52f, 8f);

        Image arrowHeadTop = CreateImage("HeadTop", arrowRoot, Color.white, false);
        ConfigureArrowHead(arrowHeadTop.rectTransform, 45f, 13f);

        Image arrowHeadBottom = CreateImage("HeadBottom", arrowRoot, Color.white, false);
        ConfigureArrowHead(arrowHeadBottom.rectTransform, -45f, -13f);

        arrowImages = new[] { shaft, arrowHeadTop, arrowHeadBottom };
        arrowObject.SetActive(false);
        return arrowRoot;
    }

    private static void ConfigureArrowHead(RectTransform rectTransform, float rotation, float y)
    {
        rectTransform.anchorMin = new Vector2(0f, 0.5f);
        rectTransform.anchorMax = new Vector2(0f, 0.5f);
        rectTransform.pivot = new Vector2(1f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(70f, y);
        rectTransform.sizeDelta = new Vector2(26f, 8f);
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
    }

    private static Image CreateImage(string name, Transform parent, Color color, bool raycastTarget)
    {
        GameObject imageObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        int separatorIndex = folderPath.LastIndexOf('/');
        string parentFolder = folderPath.Substring(0, separatorIndex);
        string folderName = folderPath.Substring(separatorIndex + 1);
        EnsureFolder(parentFolder);
        AssetDatabase.CreateFolder(parentFolder, folderName);
    }
}
