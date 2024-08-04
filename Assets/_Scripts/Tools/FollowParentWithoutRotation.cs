using UnityEngine;

public class FollowParentWithoutRotation : MonoBehaviour
{
    private Vector3 initialLocalPosition;
    public GameObject targetTransformRight;
    public GameObject targetTransformLeft;

    void Start()
    {
        // Store the initial local position of the child relative to the parent
        initialLocalPosition = transform.localPosition;
    }

    void LateUpdate()
    {
        if (transform.parent != null)
        {
            // Update the child's position to follow the parent
            transform.position = transform.parent.TransformPoint(initialLocalPosition);

            // Maintain the child's current rotation
            transform.rotation = transform.rotation;
        }
    }

    public void RotateRight()
    {
        // Rotate the child to the right by the specified angle
        gameObject.transform.rotation = targetTransformRight.transform.rotation;
        gameObject.transform.localPosition = targetTransformRight.transform.localPosition;
        initialLocalPosition = transform.localPosition;
    }

    public void RotateLeft()
    {
        // Rotate the child to the left by the specified angle
        gameObject.transform.rotation = targetTransformLeft.transform.rotation;
        gameObject.transform.localPosition = targetTransformLeft.transform.localPosition;
        initialLocalPosition = transform.localPosition;
    }

}
