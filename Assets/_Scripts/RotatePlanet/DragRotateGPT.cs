using UnityEngine;
public class DragRotateGPT : MonoBehaviour
{
    private Vector2 dragStartPos;
    private Vector2 dragEndPos;

    public float rotationSpeed = 1.0f; // Adjust the rotation speed as needed
    public float maxRotationX = 80f;   // Example maximum rotation around X axis
    public float autoRotateSpeed = 2f; // Adjust the autorotation speed

    private float currentAutoRotationSpeed = 0f;
    private Vector2 lastDragDirection;

    void Start()
    {
        // Set an initial auto-rotation speed on start
        currentAutoRotationSpeed = autoRotateSpeed;
    }

    void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    dragStartPos = touch.position;
                    currentAutoRotationSpeed = 0f; // Reset autorotation speed on new drag
                    break;

                case TouchPhase.Moved:
                    dragEndPos = touch.position;
                    Vector2 dragDelta = dragEndPos - dragStartPos;

                    float rotationX = dragDelta.y * rotationSpeed * Time.deltaTime;
                    float rotationY = -dragDelta.x * rotationSpeed * Time.deltaTime;

                    // Clamp rotation around X axis to prevent flipping
                    float currentRotationX = transform.eulerAngles.x;
                    rotationX = Mathf.Clamp(currentRotationX + rotationX, -maxRotationX, maxRotationX) - currentRotationX;

                    // Apply rotation around the object's center
                    transform.Rotate(Vector3.up, rotationY, Space.World);
                    transform.Rotate(Vector3.right, rotationX, Space.World);

                    // Store drag direction for autorotation
                    lastDragDirection = dragDelta.normalized;

                    dragStartPos = dragEndPos;
                    break;

                case TouchPhase.Ended:
                    // Set autorotation speed based on the last drag direction
                    currentAutoRotationSpeed = -lastDragDirection.x * autoRotateSpeed;
                    break;
            }
        }

        // Gradually reduce autorotation speed
        //currentAutoRotationSpeed = Mathf.Lerp(currentAutoRotationSpeed, 0f, autoRotateSpeed * Time.deltaTime);

        // Apply autorotation along the drag direction
        transform.Rotate(Vector3.up, currentAutoRotationSpeed * Time.deltaTime, Space.World);
    }
}
