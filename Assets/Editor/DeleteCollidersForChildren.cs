using UnityEngine;
using UnityEditor;

public class DeleteCollidersForChildren : EditorWindow 
{
    [MenuItem("Tools/Delete Colliders for Child Objects")]
    public static void DeleteColliders()
    {
        // Получаем выбранный объект в иерархии
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("Пожалуйста, выберите объект в иерархии.");
            return;
        }

        // Получаем все компоненты Collider во всех дочерних объектах
        Collider[] colliders = selected.GetComponentsInChildren<Collider>();
        int colliderCount = colliders.Length;

        if (colliderCount == 0)
        {
            Debug.Log("В дочерних объектах не найдено компонентов Collider.");
            return;
        }

        // Удаляем каждый найденный компонент Collider с регистрацией Undo
        foreach (Collider col in colliders)
        {
            Undo.DestroyObjectImmediate(col);
        }

        Debug.Log($"Удалено {colliderCount} компонент(ов) Collider из выбранного объекта и его дочерних объектов.");
    }
}
