using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPlaceRotator : MonoBehaviour
{
    public Transform cameraTransform;
    private DragRotateGPT dragRotateGPT;
    private List<PlanetObject> needToShowList = new List<PlanetObject>();
    private bool isRotating = false;

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
                isRotating = false; // Reset the flag after the rotation is complete
                dragRotateGPT.enabled = true;
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
        // Vector from the center of the sphere to the camera
        Vector3 a = cameraTransform.position - transform.position;

        // Vector from the center of the sphere to the object
        Vector3 b = targetObjectTransform.position - transform.position;

        // Calculate the rotation needed to align vector b with vector a
        Quaternion targetRotation = Quaternion.FromToRotation(b, a) * transform.rotation;

        // Time taken to rotate to target rotation
        float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Interpolate between the current rotation and the target rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, elapsed / duration);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure final rotation is exactly the target rotation
        transform.rotation = targetRotation;

        targetObjectTransform.gameObject.SetActive(true);
        targetObjectTransform.GetComponent<PlanetObject>().MakeSFX();

        yield return new WaitForSeconds(1f);

        // Coroutine is finished at this point
        Debug.Log("Rotation completed!");
    }
}