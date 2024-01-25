using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectActivator : MonoBehaviour
{
    public ItemController itemController;
    public List<PlanetObject> objectsToActivate = new List<PlanetObject>();
    private int currentIndex = 0;
    [SerializeField] private ObjectPlaceRotator objectPlaceRotator;
   

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
            objectPlaceRotator.AddToNeedToShowList(objectsToActivate[currentIndex]);

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