using UnityEngine;

public class ToggleChildColliders : MonoBehaviour
{
    // Boolean flag to keep track of collider state
    private bool collidersEnabled = true;

    // Function to toggle colliders of all child objects recursively
    public void ToggleCollidersRecursively(GameObject parentObject)
    {
        // Toggle colliders for immediate children
        ToggleColliders(parentObject);

        // Recursively toggle colliders for each child's children
        foreach (Transform child in parentObject.transform)
        {
            ToggleCollidersRecursively(child.gameObject);
        }
    }

    // Function to toggle colliders of a specified parent object
    private void ToggleColliders(GameObject parentObject)
    {
        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!!");
        // Get all colliders attached to the specified parent object
        Collider[] colliders = parentObject.GetComponentsInChildren<Collider>();

        // Toggle the enabled state of each collider
        foreach (Collider collider in colliders)
        {
            collider.enabled = collidersEnabled;
        }
    }

    // Function to toggle colliders (entry point for toggling)
    public void ToggleAllColliders(GameObject parentObject)
    {
        // Toggle colliders recursively starting from the specified parent object
        ToggleCollidersRecursively(parentObject);

        // Toggle the flag for the next call
        collidersEnabled = !collidersEnabled;
    }
}

