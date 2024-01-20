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
            MakeSFX(objectsToActivate[currentIndex]);
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

    private void MakeSFX(GameObject parentObject) 
    {
        GameObject sfx = ObjectPooler.SharedInstance.GetPooledObject("SFX");
        if (sfx != null)
        {
            sfx.transform.parent = null;
            sfx.transform.localScale = new Vector3(1, 1, 1);
            sfx.transform.SetParent(parentObject.transform);           
            
            sfx.transform.localPosition = Vector3.zero;
            sfx.transform.localEulerAngles = Vector3.zero;          
            sfx.SetActive(true);
           StartCoroutine(DisableSFX(sfx));
        }
    }

    private IEnumerator DisableSFX(GameObject sfx)
    {
        yield return new WaitForSeconds(1.5f);
        sfx.SetActive(false);
    }

}