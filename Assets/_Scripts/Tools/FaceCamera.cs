using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    private Camera mainCamera; 

    void Start()
    {
        // Find the main camera
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (mainCamera != null)
        {
            // Make the canvas face the camera
            transform.LookAt(mainCamera.transform);

            // Optional: reverse the direction to avoid the canvas being flipped
            transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
        }
    }
}
