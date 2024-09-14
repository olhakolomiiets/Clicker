using UnityEngine;

public class DragRotateGPT2 : MonoBehaviour
{
    private Vector2 dragStartPos;
    private Vector2 dragEndPos;

    public float rotationSpeed = 1.0f; // Adjust the rotation speed as needed
    public float maxRotationX = 80f;   // Example maximum rotation around X axis
    public float autoRotateSpeed = 20f; // Adjust the autorotation speed
    public float autoRotateDecayRate = 1.5f; // Rate at which auto-rotation slows down

    private float currentAutoRotationSpeed;
    private Vector2 lastDragDirection;
    private bool isDragging = false;
    

    void Start()
    {
        // Start auto-rotation immediately at the beginning
        currentAutoRotationSpeed = autoRotateSpeed;
    }

    void Update()
    {
        // Handle both touch and mouse input
        if (Input.touchCount > 0)
        {
            HandleTouchInput();
        }
        else if (Input.GetMouseButton(0)) // Left mouse button pressed
        {
            HandleMouseInput();
        }
        else if (!isDragging) // Auto-rotation should only work when not dragging
        {
            AutoRotate();
        }

        // Gradually reduce auto-rotation speed after drag ends
        currentAutoRotationSpeed = Mathf.Lerp(currentAutoRotationSpeed, autoRotateSpeed, autoRotateDecayRate * Time.deltaTime);
    }

    // Method to handle touch input
    void HandleTouchInput()
    {
        Touch touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                dragStartPos = touch.position;
                currentAutoRotationSpeed = 0f; // Stop auto-rotation during drag
                isDragging = true; // Flag that we are dragging
                break;

            case TouchPhase.Moved:
                dragEndPos = touch.position;
                Vector2 dragDelta = dragEndPos - dragStartPos;

                RotateObject(dragDelta);

                // Store drag direction for auto-rotation
                lastDragDirection = dragDelta.normalized;

                // Update the start position for smooth dragging
                dragStartPos = dragEndPos;
                break;

            case TouchPhase.Ended:
                // Set auto-rotation speed based on the last drag direction
                currentAutoRotationSpeed = -lastDragDirection.x * autoRotateSpeed;
                isDragging = false; // Dragging has ended
                break;
        }
    }

    // Method to handle mouse input
    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0)) // On mouse button press
        {
            dragStartPos = Input.mousePosition;
            currentAutoRotationSpeed = 0f; // Stop auto-rotation during drag
            isDragging = true; // Flag that we are dragging
        }

        if (Input.GetMouseButton(0)) // While holding the mouse button
        {
            dragEndPos = Input.mousePosition;
            Vector2 dragDelta = dragEndPos - dragStartPos;

            RotateObject(dragDelta);

            // Store drag direction for auto-rotation
            lastDragDirection = dragDelta.normalized;

            // Update the start position for smooth dragging
            dragStartPos = dragEndPos;
        }

        if (Input.GetMouseButtonUp(0)) // On mouse button release
        {
            // Set auto-rotation speed based on the last drag direction
            currentAutoRotationSpeed = -lastDragDirection.x * autoRotateSpeed;
            isDragging = false; // Dragging has ended
        }
    }

    // Method to rotate the object based on drag delta
    void RotateObject(Vector2 dragDelta)
    {
        float rotationX = dragDelta.y * rotationSpeed * Time.deltaTime;
        float rotationY = -dragDelta.x * rotationSpeed * Time.deltaTime;

        // Clamp rotation around the X-axis to prevent flipping
        float currentRotationX = transform.eulerAngles.x;
        float newRotationX = Mathf.Clamp(currentRotationX + rotationX, -maxRotationX, maxRotationX);

        // Apply rotation
        transform.Rotate(Vector3.up, rotationY, Space.World);
        transform.eulerAngles = new Vector3(newRotationX, transform.eulerAngles.y, transform.eulerAngles.z);
    }

    // Method for auto-rotating the object
    void AutoRotate()
    {
        // Apply constant auto-rotation when not interacting
        transform.Rotate(Vector3.up, currentAutoRotationSpeed * Time.deltaTime, Space.World);
    }
}
