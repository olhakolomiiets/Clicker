using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessRotation : MonoBehaviour
{
    public float rotationDuration = 2f; // Duration for one complete rotation
    public Vector3 rotationAxis = Vector3.up; // Axis of rotation

    void Start()
    {
        // Start endless rotation
        StartEndlessRotation();
    }

    void StartEndlessRotation()
    {
        // Rotate the object infinitely around the specified axis
        transform.DORotate(new Vector3(0f, 360f, 0f), rotationDuration, RotateMode.LocalAxisAdd)
            .SetLoops(-1, LoopType.Incremental)
            .SetEase(Ease.Linear); // Linear easing for smooth rotation
    }
}