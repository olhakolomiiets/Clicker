using UnityEditor;
using UnityEngine;

public class EnableDisableChildren : MonoBehaviour
{
    [MenuItem("GameObject/Enable Direct Children", false, 0)]
    private static void EnableDirectChildren()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            SetDirectChildrenActive(obj.transform, true);
        }
    }

    [MenuItem("GameObject/Disable Direct Children", false, 1)]
    private static void DisableDirectChildren()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            SetDirectChildrenActive(obj.transform, false);
        }
    }

    private static void SetDirectChildrenActive(Transform parent, bool state)
    {
        foreach (Transform child in parent)
        {
            child.gameObject.SetActive(state); // Только прямой ребёнок
        }
    }

    [MenuItem("GameObject/Enable Direct Children", true)]
    [MenuItem("GameObject/Disable Direct Children", true)]
    private static bool ValidateSelection()
    {
        return Selection.gameObjects.Length > 0;
    }
}
