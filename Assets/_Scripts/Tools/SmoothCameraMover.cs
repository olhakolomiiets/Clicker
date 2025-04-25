using UnityEngine;
using UnityEditor;
using System.Collections;

[ExecuteInEditMode]
public class SmoothCameraMover : MonoBehaviour
{
    public Transform targetTransform;
    public float moveDuration = 2f;
    public Camera targetCamera;
    public bool alsoRotate = true;

    private Coroutine moveCoroutine;

    public void MoveCamera()
    {
        if (targetCamera == null || targetTransform == null)
        {
            Debug.LogWarning("Target Camera or Target Transform is not assigned.");
            return;
        }

        if (Application.isPlaying)
        {
            if (moveCoroutine != null)
                StopCoroutine(moveCoroutine);

            moveCoroutine = StartCoroutine(MoveOverTime());
        }
        else
        {
            // В редакторе просто телепортируем
            targetCamera.transform.position = targetTransform.position;
            if (alsoRotate)
                targetCamera.transform.rotation = targetTransform.rotation;
        }
    }

    private IEnumerator MoveOverTime()
    {
        Transform camTransform = targetCamera.transform;
        Vector3 startPos = camTransform.position;
        Quaternion startRot = camTransform.rotation;

        Vector3 endPos = targetTransform.position;
        Quaternion endRot = targetTransform.rotation;

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            float t = elapsed / moveDuration;
            camTransform.position = Vector3.Lerp(startPos, endPos, t);
            if (alsoRotate)
                camTransform.rotation = Quaternion.Slerp(startRot, endRot, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        camTransform.position = endPos;
        if (alsoRotate)
            camTransform.rotation = endRot;
    }
}

[CustomEditor(typeof(SmoothCameraMover))]
public class SmoothCameraMoverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SmoothCameraMover mover = (SmoothCameraMover)target;

        if (GUILayout.Button("Move Camera"))
        {
            mover.MoveCamera();
        }
    }
}
