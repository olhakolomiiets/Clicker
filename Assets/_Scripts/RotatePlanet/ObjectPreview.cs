using UnityEngine;

public class ObjectPreview : MonoBehaviour
{
    private const short ONE = 1;
    [SerializeField] private float xSensitivity = 50;
    [SerializeField] private float ySensitivity = 50;
    private Vector3 mousePreviusPos = Vector3.zero;
    private Vector3 mouseDelta = Vector3.zero;

    private void Update()
    {
        if (Input.GetMouseButton(ushort.MinValue))
        {
            mouseDelta = Input.mousePosition - mousePreviusPos;
            float value = Vector3.Dot(transform.up, Vector3.up) >= ushort.MinValue ? -ONE : ONE;
            transform.Rotate(transform.up, Vector3.Dot(mouseDelta, Camera.main.transform.right) * value * xSensitivity * Time.deltaTime, Space.World);
            transform.Rotate(Camera.main.transform.right, Vector3.Dot(mouseDelta, Camera.main.transform.up) * ySensitivity * Time.deltaTime, Space.World);
        }
        mousePreviusPos = Input.mousePosition;
    }
}