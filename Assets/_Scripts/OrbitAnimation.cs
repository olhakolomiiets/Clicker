using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrbitAnimation : MonoBehaviour
{
    float timeCounter = 0;

    [Header("Orbit Settings")]
    [SerializeField] float speed;
    [SerializeField] float width;
    [SerializeField] float height;
    [Header("Rotation Settings")]
    [SerializeField] float rotationSpeed;


    void Start()
    {
        
    }

    void Update()
    {
        timeCounter += Time.deltaTime * speed;
        float x = Mathf.Cos(timeCounter) * width;
        float y = Mathf.Sin(timeCounter) * height;
        float z = 0;

        transform.localPosition = new Vector3(x,y,z);
        //transform.rotation = Quaternion.Euler(0, 90, 0);


        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
    }
}
