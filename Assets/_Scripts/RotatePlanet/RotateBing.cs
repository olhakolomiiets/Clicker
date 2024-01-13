using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateBing : MonoBehaviour
{
    private Vector2 _fingerDownPosition;
    private Vector2 _fingerUpPosition;

    [SerializeField]
    private float _minSwipeDistance = 30f;

    [SerializeField]
    private float _rotationSpeed = 0.2f;

    [SerializeField]
    private float _speedRotation = 1f / 3f;

    private void Update()
    {
        if (Input.touchCount == 1)
        {
            var touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                _fingerDownPosition = touch.position;
            }

            if (touch.phase == TouchPhase.Ended)
            {
                _fingerUpPosition = touch.position;

                if (Vector2.Distance(_fingerDownPosition, _fingerUpPosition) > _minSwipeDistance)
                {
                    var rotationVector = new Vector3(_fingerUpPosition.y - _fingerDownPosition.y, _fingerDownPosition.x - _fingerUpPosition.x, 0);
                    transform.rotation *= Quaternion.AngleAxis(rotationVector.magnitude * _rotationSpeed * _speedRotation, rotationVector.normalized);
                    transform.rotation = Quaternion.Inverse(transform.rotation);
                }
            }
        }
    }
}