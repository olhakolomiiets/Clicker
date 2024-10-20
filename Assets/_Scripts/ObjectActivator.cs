using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ObjectActivator : MonoBehaviour
{
    public ItemController itemController;
    public UpgradeItemController upgradeItemController;
    public List<PlanetObject> objectsToActivate = new();
    [SerializeField] private int currentIndex = 0;
    [SerializeField] private ObjectPlaceRotator objectPlaceRotator;

    private bool isDisplayed;

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

        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! ObjectActivator /// ActivateNextObject");
    }

    public void ActivatePurchasedObject(int itemCount)
    {
        for (int i = 0; i < itemCount; i++)
        {
            Animator animator = objectsToActivate[i].gameObject.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = false;
            }
            objectsToActivate[i].gameObject.SetActive(true);
        }

        currentIndex = itemCount;
    }

    public void PlanetObjectsToggle()
    {
        foreach (var obj in objectsToActivate)
        {
            obj.gameObject.SetActive(!isDisplayed);
        }
        isDisplayed = !isDisplayed;
    }

}