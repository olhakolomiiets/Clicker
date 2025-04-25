using UnityEngine;
using UnityEditor;

public class DeleteChildrenByNameEditor : EditorWindow
{
    private string searchString = "";

    [MenuItem("Tools/Delete Child Objects By Name")]
    static void Init()
    {
        DeleteChildrenByNameEditor window = (DeleteChildrenByNameEditor)EditorWindow.GetWindow(typeof(DeleteChildrenByNameEditor));
        window.titleContent = new GUIContent("Delete Children");
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Удалить дочерние объекты по имени", EditorStyles.boldLabel);
        searchString = EditorGUILayout.TextField("Содержит:", searchString);

        if (GUILayout.Button("Удалить из выделенного объекта"))
        {
            DeleteMatchingChildren();
        }
    }

    void DeleteMatchingChildren()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("Ничего не выбрано.");
            return;
        }

        Transform parent = Selection.activeGameObject.transform;
        int count = 0;

        for (int i = parent.childCount - 1; i >= 0; i--) // снизу вверх
        {
            Transform child = parent.GetChild(i);
            if (child.name.Contains(searchString))
            {
                DestroyImmediate(child.gameObject);
                count++;
            }
        }

        Debug.Log($"Удалено объектов: {count}");
    }
}
