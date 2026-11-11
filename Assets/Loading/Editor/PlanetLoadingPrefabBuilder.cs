#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PlanetBuilder.Loading.Editor
{
    public static class PlanetLoadingPrefabBuilder
    {
        private const string Root = "Assets/PlanetBuilder/Loading";
        private const string PrefabPath = Root + "/Prefabs/PlanetLoadingScreen.prefab";
        private static readonly string[] SpritePaths =
        {
            Root + "/Sprites/Stage_01_Grass.png",
            Root + "/Sprites/Stage_02_Trees.png",
            Root + "/Sprites/Stage_03_Medieval.png",
            Root + "/Sprites/Stage_04_Industry.png",
            Root + "/Sprites/Stage_05_City.png",
            Root + "/Sprites/Stage_06_Future.png"
        };

        [MenuItem("Tools/PlanetBuilder/Loading/1 - Create or Update Prefab")]
        public static void CreatePrefabFromMenu()
        {
            CreatePrefab();
        }

        public static GameObject CreatePrefab()
        {
            Sprite[] planetSprites = new Sprite[SpritePaths.Length];
            for (int i = 0; i < SpritePaths.Length; i++)
            {
                planetSprites[i] = LoadUISprite(SpritePaths[i]);
                if (planetSprites[i] == null)
                {
                    Debug.LogError("[PlanetBuilder] Missing loading image: " + SpritePaths[i]);
                    return null;
                }
            }

            Sprite backgroundSprite = LoadUISprite(Root + "/Background/SpaceBackground.png");
            if (backgroundSprite == null)
            {
                Debug.LogError("[PlanetBuilder] Space background sprite is missing.");
                return null;
            }

            GameObject root = null;
            try
            {
                root = new GameObject("PlanetLoadingScreen",
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                    typeof(GraphicRaycaster), typeof(CanvasGroup));
                root.layer = LayerMask.NameToLayer("UI");

                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 32000;
                canvas.pixelPerfect = false;
                canvas.enabled = false; // hidden until Show() is called

                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                GraphicRaycaster raycaster = root.GetComponent<GraphicRaycaster>();
                raycaster.enabled = false;
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;

                Image background = CreateImage("Background", root.transform);
                Stretch(background.rectTransform);
                background.sprite = backgroundSprite;
                background.color = Color.white;
                background.preserveAspect = false;
                background.raycastTarget = true; // block clicks on the old/new scene

                Image planet = CreateImage("PlanetStageImage", root.transform);
                Stretch(planet.rectTransform);
                planet.sprite = planetSprites[0];
                planet.color = Color.white;
                planet.preserveAspect = true;
                planet.raycastTarget = false;

                var markerParent = new GameObject("StageIndicators", typeof(RectTransform));
                markerParent.layer = root.layer;
                markerParent.transform.SetParent(root.transform, false);
                var markerRect = markerParent.GetComponent<RectTransform>();
                markerRect.anchorMin = new Vector2(0.5f, 0f);
                markerRect.anchorMax = new Vector2(0.5f, 0f);
                markerRect.pivot = new Vector2(0.5f, 0.5f);
                markerRect.anchoredPosition = new Vector2(0f, 140f);
                markerRect.sizeDelta = new Vector2(520f, 28f);
                markerRect.localScale = Vector3.one;

                Image[] markers = new Image[6];
                for (int i = 0; i < markers.Length; i++)
                {
                    markers[i] = CreateImage($"StageMarker_{i + 1:00}", markerParent.transform);
                    markers[i].sprite = null; // built-in white UI texture, tinted by color
                    markers[i].color = i == 0
                        ? new Color(0.30f, 0.91f, 1f, 1f)
                        : new Color(0.40f, 0.54f, 0.70f, 0.45f);
                    markers[i].raycastTarget = false;
                    var r = markers[i].rectTransform;
                    r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                    r.pivot = new Vector2(0.5f, 0.5f);
                    r.anchoredPosition = new Vector2((i - 2.5f) * 84f, 0f);
                    r.sizeDelta = new Vector2(64f, 8f);
                    r.localScale = Vector3.one;
                }

                PlanetLoadingScreen controller = root.AddComponent<PlanetLoadingScreen>();
                SerializedObject so = new SerializedObject(controller);
                so.FindProperty("overlayCanvas").objectReferenceValue = canvas;
                so.FindProperty("canvasGroup").objectReferenceValue = group;
                so.FindProperty("raycaster").objectReferenceValue = raycaster;
                so.FindProperty("planetImage").objectReferenceValue = planet;
                so.FindProperty("stageInterval").floatValue = 0.55f;
                so.FindProperty("finalStageHold").floatValue = 0.80f;
                so.FindProperty("fadeOutSeconds").floatValue = 0.15f;

                SerializedProperty stages = so.FindProperty("planetStages");
                stages.arraySize = 6;
                SerializedProperty markerArray = so.FindProperty("stageMarkers");
                markerArray.arraySize = 6;
                for (int i = 0; i < 6; i++)
                {
                    stages.GetArrayElementAtIndex(i).objectReferenceValue = planetSprites[i];
                    markerArray.GetArrayElementAtIndex(i).objectReferenceValue = markers[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                // Empty folders are not preserved by ZIP extraction. Create output folder if needed.
                if (!AssetDatabase.IsValidFolder(Root + "/Prefabs"))
                    AssetDatabase.CreateFolder(Root, "Prefabs");

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool succeeded);
                if (!succeeded || prefab == null)
                {
                    Debug.LogError("[PlanetBuilder] Failed to write prefab at " + PrefabPath);
                    return null;
                }

                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("[PlanetBuilder] READY: " + PrefabPath + " | 6 sprites | 0.55s + 0.80s | loop 3.55s");
                return prefab;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                return null;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        [MenuItem("Tools/PlanetBuilder/Loading/2 - Create Prefab AND Add to Open Scene")]
        public static void CreateAndAddToScene()
        {
            GameObject prefab = CreatePrefab();
            if (prefab == null) return;
            if (Object.FindFirstObjectByType<PlanetLoadingScreen>() != null)
            {
                Debug.LogWarning("[PlanetBuilder] Open scene already contains PlanetLoadingScreen. No duplicate added.");
                return;
            }
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                Debug.LogError("[PlanetBuilder] Could not instantiate the loading prefab.");
                return;
            }
            Undo.RegisterCreatedObjectUndo(instance, "Add Planet Loading Screen");
            EditorSceneManager.MarkSceneDirty(instance.scene);
            Selection.activeGameObject = instance;
            Debug.Log("[PlanetBuilder] Loading screen added to the active scene. Save the scene (Ctrl+S).");
        }

        private static Sprite LoadUISprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            bool needsImport = importer.textureType != TextureImporterType.Sprite
                               || importer.spriteImportMode != SpriteImportMode.Single
                               || importer.mipmapEnabled
                               || importer.maxTextureSize < 2048
                               || importer.textureCompression != TextureImporterCompression.CompressedHQ
                               || importer.npotScale != TextureImporterNPOTScale.None;

            if (needsImport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.compressionQuality = 80;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
#endif
