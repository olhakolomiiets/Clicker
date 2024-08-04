using UnityEngine;

public class TransformChanger : MonoBehaviour
{
    public GameObject target; // Assign the target GameObject in the Inspector

    public GameObject targetTransformToChange;
    public GameObject targetTransformToChange2;
    public bool isChanging;
    private Vector3 initialTargetPosition;
    private Vector3 initialPositionOffset;
    private Vector3 initialTargetScale;
    private Vector3 initialScaleMultiplier;

    void Start()
    {
        isChanging = true;
        if (target != null)
        {
            // Store the initial offset and scale multiplier
            initialTargetPosition = target.transform.position;
            initialPositionOffset = transform.position - target.transform.position;
            initialTargetScale = target.transform.localScale;
            initialScaleMultiplier = new Vector3(
                transform.localScale.x / target.transform.localScale.x,
                transform.localScale.y / target.transform.localScale.y,
                transform.localScale.z / target.transform.localScale.z
            );
        }
    }

    void Update()
    {
        if (target != null && isChanging)
        {
            // Update position relative to the target's position changes
            Vector3 positionChange = target.transform.position - initialTargetPosition;
            transform.position = initialTargetPosition + initialPositionOffset + positionChange;

            // Update scale relative to the target's scale changes
            Vector3 scaleChange = new Vector3(
                target.transform.localScale.x / initialTargetScale.x,
                target.transform.localScale.y / initialTargetScale.y,
                target.transform.localScale.z / initialTargetScale.z
            );
            transform.localScale = new Vector3(
                initialScaleMultiplier.x * scaleChange.x,
                initialScaleMultiplier.y * scaleChange.y,
                initialScaleMultiplier.z * scaleChange.z
            );
        }
    }

    public void SetObjectToTargetTransform(int targetGO)
    {
        isChanging = false;
        switch (targetGO)
        {
            case 0:
                // Actions for the first checkpoint
                Debug.Log("Reached checkpoint 0");

                break;

            case 1:
                gameObject.transform.position = targetTransformToChange.transform.position;
                isChanging = true;
                Debug.Log("Reached checkpoint 1");               
                break;

            case 2:
                gameObject.transform.position = targetTransformToChange2.transform.position;
                isChanging = true;
                Debug.Log("Reached checkpoint 2");
                break;

            default:
                Debug.Log("Reached an undefined checkpoint");
                break;
        }
    }
}
