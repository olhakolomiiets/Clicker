using UnityEngine;
using UnityEditor;

public class WrapInPrefabTool : EditorWindow
{
    private GameObject prefabToWrap;
    private const string PrefKey = "WrapInPrefab_LastUsedPrefab";

    [MenuItem("Tools/Wrap Selected Objects In Prefab")]
    static void Init()
    {
        WrapInPrefabTool window = (WrapInPrefabTool)EditorWindow.GetWindow(typeof(WrapInPrefabTool));
        window.titleContent = new GUIContent("Wrap In Prefab");
        window.Show();
    }

    void OnEnable()
    {
        string path = EditorPrefs.GetString(PrefKey, "");
        if (!string.IsNullOrEmpty(path))
        {
            prefabToWrap = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }

    void OnGUI()
    {
        GUILayout.Label("Обернуть объекты в структуру префаба", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        prefabToWrap = (GameObject)EditorGUILayout.ObjectField("Prefab (структура):", prefabToWrap, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck() && prefabToWrap != null)
        {
            string prefabPath = AssetDatabase.GetAssetPath(prefabToWrap);
            EditorPrefs.SetString(PrefKey, prefabPath);
        }

        if (GUILayout.Button("Обернуть выделенные объекты"))
        {
            WrapSelectedObjects();
        }
    }

    void WrapSelectedObjects()
    {
        if (prefabToWrap == null)
        {
            Debug.LogError("Префаб не задан.");
            return;
        }

        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects.Length == 0)
        {
            Debug.LogError("Нет выделенных объектов.");
            return;
        }

        // Создаём префаб в сцене
        GameObject prefabInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabToWrap);
        prefabInstance.transform.position = selectedObjects[0].transform.position;
        prefabInstance.transform.rotation = selectedObjects[0].transform.rotation;

        // Находим самый глубоко вложенный объект с именем "Object"
        Transform targetContainer = FindDeepestNamedChild(prefabInstance.transform, "Object");

        if (targetContainer == null)
        {
            Debug.LogError("В префабе не найден вложенный 'Object'.");
            DestroyImmediate(prefabInstance);
            return;
        }

        // Перемещаем все выделенные объекты в этот контейнер
        foreach (GameObject obj in selectedObjects)
        {
            Vector3 worldPos = obj.transform.position;
            Quaternion worldRot = obj.transform.rotation;

            obj.transform.SetParent(targetContainer);
            obj.transform.position = worldPos;
            obj.transform.rotation = worldRot;
        }

        Selection.activeGameObject = prefabInstance;
        Debug.Log($"Объектов обернуто: {selectedObjects.Length}");
    }

    // Поиск самого вложенного child с заданным именем
    Transform FindDeepestNamedChild(Transform parent, string name)
    {
        Transform deepest = null;
        int maxDepth = -1;

        void Traverse(Transform current, int depth)
        {
            if (current.name == name && depth > maxDepth)
            {
                deepest = current;
                maxDepth = depth;
            }

            foreach (Transform child in current)
            {
                Traverse(child, depth + 1);
            }
        }

        Traverse(parent, 0);
        return deepest;
    }
}
