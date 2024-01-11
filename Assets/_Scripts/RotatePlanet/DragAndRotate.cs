using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DragAndRotate : MonoBehaviour
{
    public bool isActive = false;
    Color activeColor = new Color();

    void Update()
    {

            if (Input.touchCount == 1)
            {
                Touch screenTouch = Input.GetTouch(0);
                if (screenTouch.phase == TouchPhase.Moved)
                {
                    transform.Rotate(0f, screenTouch.deltaPosition.x, 0f);
                }
            }
    }
}