using UnityEngine;

public class DisableGPUInstancing : MonoBehaviour
{
    void Awake()
    {
        // Find all renderers in the scene
        Renderer[] renderers = FindObjectsOfType<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            // Get materials from the renderer
            Material[] materials = renderer.sharedMaterials;

            // Loop through materials and disable GPU instancing
            foreach (Material material in materials)
            {
                material.enableInstancing = false;
            }
        }
    }
}
