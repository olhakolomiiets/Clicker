using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActivateDisableWithTimer : MonoBehaviour 
{
    public GameObject targetObject;
    public GameObject vfx;
    public float delayAfterVFX; 
    float activeDuration; // Set the duration in seconds
    float disableDuration; // Set the duration in seconds 
    private bool isActivated = false;
    [Header("Random Settings")]
    public float minStartDelay; // Minimum delay before activation on start
    public float maxStartDelay; // Maximum delay before activation on start
    [Space(10)]
    public float minActiveDuration; // Minimum duration of activation
    public float maxActiveDuration; // Maximum duration of activation
    [Space(10)]
    public float minDisableDuration; // Minimum duration of activation
    public float maxDisableDuration; // Maximum duration of activation


    void Start()
    {
        // Initial setup, deactivate the GameObject
        activeDuration = Random.Range(minActiveDuration, maxActiveDuration);
        disableDuration = Random.Range(minDisableDuration, maxDisableDuration);
        targetObject.SetActive(false);

        // Start the random delay before activating the GameObject
        float randomDelay = Random.Range(minStartDelay, maxStartDelay);
        Invoke("ActivateWithRandomDelay", randomDelay);
    }

    void Update()
    {
        if (isActivated)
        {
            activeDuration -= Time.deltaTime;

            if (activeDuration <= 0f)
            {
                // If the duration has elapsed, deactivate the GameObject
                DeactivateGameObject();
            }
        }
    }

    // Activate the GameObject for the specified duration
    private void ActivateWithRandomDelay()
    {
        vfx.SetActive(true);
        StartCoroutine("ActivateGameObjectRoutine");
    }

    // Activate the GameObject for the specified duration
    public void ActivateGameObject()
    {
        targetObject.SetActive(true);
        isActivated = true;
    }

    public IEnumerator ActivateGameObjectRoutine()
    {
        yield return new WaitForSeconds(delayAfterVFX);
        targetObject.SetActive(true);
        isActivated = true;
        yield return new WaitForSeconds(2f);
        vfx.SetActive(false);
    }

    public IEnumerator DisableGameObjectRoutine() 
    {
        yield return new WaitForSeconds(delayAfterVFX);
        targetObject.SetActive(false);
        isActivated = false;
        vfx.SetActive(false);
    }

    // Deactivate the GameObject
    private void DeactivateGameObject()
    {
        vfx.SetActive(true);
        StartCoroutine("DisableGameObjectRoutine");       
        disableDuration = Random.Range(minDisableDuration, maxDisableDuration);
        Invoke("SetUpRandomActiveDuration", disableDuration);
    }

    private void SetUpRandomActiveDuration()
    {
        activeDuration = Random.Range(minActiveDuration, maxActiveDuration);
        vfx.SetActive(true);
        StartCoroutine("ActivateGameObjectRoutine");
    }
}