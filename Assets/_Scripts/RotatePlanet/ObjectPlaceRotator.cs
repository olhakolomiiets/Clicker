using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPlaceRotator : MonoBehaviour
{
    public Transform cameraTransform;
    private DragRotateGPT dragRotateGPT;
    private List<PlanetObject> needToShowList = new List<PlanetObject>();
    public bool isRotating = false;
    [SerializeField] private UIController _uiController;

    private void Start()
    {
        dragRotateGPT = GetComponent<DragRotateGPT>();
    }

    public void AddToNeedToShowList(PlanetObject planetObject)
    {
        needToShowList.Add(planetObject);

        if (!isRotating)
        {
            StartRotatingObjects();
        }
    }

    // Public method to rotate all objects in the list continuously
    public void StartRotatingObjects()
    {
        if (!isRotating && needToShowList.Count > 0)
        {
            dragRotateGPT.enabled = false;
            StartCoroutine(RotateObjects(() =>
            {
                if (!_uiController.isDisplayed)
                {
                    isRotating = false; // Reset the flag after the rotation is complete
                    dragRotateGPT.enabled = true;
                }
            }));
        }
    }

    private IEnumerator RotateObjects(Action rotationCompleteCallback)
    {
        isRotating = true;

        while (needToShowList.Count > 0)
        {
            PlanetObject currentObject = needToShowList[0];
            needToShowList.RemoveAt(0);

            if (currentObject != null)
            {
                // Rotate towards the current object's rotation
                yield return StartCoroutine(RotateToTargetRotation(currentObject.transform));
            }
        }

        rotationCompleteCallback?.Invoke(); // Invoke the callback when the rotation is complete
        Debug.Log("All rotations completed!");
    }

    public void RotateToTarget(Transform targetObjectTransform)
    {
        StartCoroutine(RotateToTargetRotation(targetObjectTransform));
    }

    private IEnumerator RotateToTargetRotation(Transform targetObjectTransform)
    {
        var cameraTransformPositionWithCorrect = cameraTransform.position + Vector3.up * 120f;

        Vector3 a = cameraTransformPositionWithCorrect - transform.position;
        Vector3 b = targetObjectTransform.position - transform.position;

        // Calculate the rotation needed to align vector b with vector a
        Quaternion targetRotation = Quaternion.FromToRotation(b, a) * transform.rotation;

        // Add degrees rotation on the axises
        targetRotation *= Quaternion.Euler(0f, 0f, 0f);

        float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float interpolationFactor = Mathf.Clamp01(elapsed / duration);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, interpolationFactor);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRotation;

        targetObjectTransform.gameObject.SetActive(true);
        targetObjectTransform.GetComponent<PlanetObject>().MakeSFX();

        yield return new WaitForSecondsRealtime(1f);

        Debug.Log("Rotation completed!");
    }
}