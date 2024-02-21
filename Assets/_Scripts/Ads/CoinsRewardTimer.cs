using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoinsRewardTimer : MonoBehaviour
{
    [SerializeField] private GameObject advertisingObject;
    [SerializeField] private float activationInterval;
    public bool isAdvertisingActive;

    void Start()
    {
        StartCoroutine(ActivateAdvertising());
    }
    IEnumerator ActivateAdvertising()
    {
        while (true)
        {
            yield return new WaitForSeconds(activationInterval * 60);

            if (!isAdvertisingActive)
            {
                ActivateAdvertisingObject();
            }
        }
    }
    void ActivateAdvertisingObject()
    {
        advertisingObject.SetActive(true);
        isAdvertisingActive = true;
    }
}
