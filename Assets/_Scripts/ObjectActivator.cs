using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectActivator : MonoBehaviour
{
    public ItemController itemController;
    public List<GameObject> objectsToActivate = new List<GameObject>();
    private int currentIndex = 0;

    void Start()
    {
        itemController.OnBuyButtonClicked += OnByButtonClicked;
    }

    private void OnByButtonClicked()
    {
        ActivateNextObject();
    }

    public void ActivateNextObject()
    {
        // Ensure that the index is within the bounds of the list
        if (currentIndex < objectsToActivate.Count)
        {
            // Activate the current object
            objectsToActivate[currentIndex].SetActive(true);

            // Increment the index for the next call
            currentIndex++;
        }
        else
        {
            // If the end of the list is reached, reset the index to restart the sequence
            currentIndex = 0;
        }
    }
    void OnDisable()
    {
        itemController.OnBuyButtonClicked -= OnByButtonClicked;
    }

}