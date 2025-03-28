using UnityEngine;
using UnityEditor;

public class EnableGPUInstancingForChildren : EditorWindow
{
    [MenuItem("Tools/Enable GPU Instancing for Child Objects")]
    static void EnableInstancing()
    {
        // Получаем выбранный объект в Hierarchy
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("Пожалуйста, выберите объект в иерархии.");
            return;
        }

        // Получаем все MeshRenderer во всех дочерних объектах
        MeshRenderer[] renderers = selected.GetComponentsInChildren<MeshRenderer>();
        int materialsModified = 0;

        foreach (MeshRenderer renderer in renderers)
        {
            // Проходимся по каждому материалу, используемому рендерером
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat != null && !mat.enableInstancing)
                {
                    // Включаем GPU Instancing
                    mat.enableInstancing = true;
                    materialsModified++;
                }
            }
        }
        Debug.Log($"Включено GPU Instancing для {materialsModified} материалов в дочерних объектах.");
    }
}
