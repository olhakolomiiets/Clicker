using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DragAndRotate2 : MonoBehaviour
{
    public float rotationSpeed = 1.0f; // Adjust the sensitivity 

    void Update()
    {
        if (Input.touchCount == 1)
        {
            Touch screenTouch = Input.GetTouch(0);

            if (screenTouch.phase == TouchPhase.Moved)
            {
                // Get the direction vector of touch movement
                Vector2 touchDeltaPosition = screenTouch.deltaPosition;

                // Calculate rotation amount based on the direction
                float rotationAmount = Mathf.Atan2(touchDeltaPosition.x, touchDeltaPosition.y) * Mathf.Rad2Deg;

                // Apply the rotation to the object
                transform.Rotate(0f, rotationAmount * rotationSpeed * Time.deltaTime, 0f);
            }
        }
    }
}