using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrbitAnimation : MonoBehaviour
{
    float timeCounter = 0;
    public Transform orbitTransform;

    [Header("Orbit Settings")]
    [SerializeField] float speed;
    [SerializeField] float width;
    [SerializeField] float height;
    [Header("Rotation Settings")]
    [SerializeField] float rotationSpeed;

    [Header("Additional Settings")]
    [SerializeField] bool alwaysLookAtCenter; // Bool for always looking at the center of the orbit
    [SerializeField] bool rotateObject;       // Bool for enabling/disabling the rotation of the object itself
    [SerializeField] bool invertOrbitRotation;   // Bool for inverting the rotation around the circular orbit

    [SerializeField] bool randomOrbit;
    [SerializeField] float minRotationXOrbit;
    [SerializeField] float maxRotationXOrbit; 

    void Start()
    {
        if (randomOrbit)
        {
            orbitTransform.Rotate(0, Random.Range(minRotationXOrbit, maxRotationXOrbit), 0); 
        }
    }

    void Update()
    {
        float rotationDirection = invertOrbitRotation ? -1f : 1f;

        // Calculate the position for the circular orbit
        timeCounter += Time.deltaTime * speed * rotationDirection;
        float x = Mathf.Cos(timeCounter) * width;
        float y = Mathf.Sin(timeCounter) * height;
        float z = 0;

        transform.localPosition = new Vector3(x, y, z);

        if (alwaysLookAtCenter)
        {
            // Make the object always look at the center of the orbit
            transform.LookAt(Vector3.zero);
        }

        if (rotateObject)
        {
            // Rotate the object around its Y-axis
            transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
        }
    }
}
