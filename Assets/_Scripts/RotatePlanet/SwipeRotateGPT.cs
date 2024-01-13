using UnityEngine;

public class SwipeRotateGPT : MonoBehaviour 
{
    private Vector2 touchStartPos;
    public float rotationSpeed = 1.0f; // Adjust the rotation speed as needed

    void Update()
    {
        // Check for touch input
        if (Input.touchCount > 0 && Input.touchCount < 2)
        {
            Touch touch = Input.GetTouch(0);

            // Check for touch phase
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    // Record the starting touch position
                    touchStartPos = touch.position;
                    break;

                case TouchPhase.Moved:
                    // Calculate swipe direction
                    Vector2 swipeDelta = touch.position - touchStartPos;

                    // Calculate rotation angles based on swipe direction
                    float rotationX = swipeDelta.y * rotationSpeed * Time.deltaTime;
                    float rotationY = -swipeDelta.x * rotationSpeed * Time.deltaTime;
                    float rotationZ = swipeDelta.x * rotationSpeed * Time.deltaTime; // Adjust the axis and sign as needed

                    // Apply rotation to the object
                    transform.Rotate(rotationX, rotationY, rotationZ);
                    break;
            }
        }
    }
}