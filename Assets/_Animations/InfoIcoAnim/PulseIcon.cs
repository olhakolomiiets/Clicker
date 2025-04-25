using System;
using UnityEngine;

public class PulseIcon : MonoBehaviour
{
    [SerializeField] float speed = 2f;
    [SerializeField] float amplitude = 0.1f;
    Vector3 baseScale;

    void Awake() => baseScale = transform.localScale;

    void Update()
    {
        float s = 1 + Mathf.Sin(Time.time * speed) * amplitude;
        transform.localScale = baseScale * s;
    }
}
