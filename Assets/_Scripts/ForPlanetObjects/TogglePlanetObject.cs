using UnityEngine;

public class TogglePlanetObject : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PlanetObject planetObject = other.GetComponent<PlanetObject>();
        if (planetObject != null)
        {
            Debug.Log("________________ENTERED____________");
            Transform childTransform = other.gameObject.transform.GetChild(0);
            if (childTransform != null && childTransform.gameObject != null)
            {
                childTransform.gameObject.SetActive(false);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlanetObject planetObject = other.GetComponent<PlanetObject>();
        if (planetObject != null)
        {
            Transform childTransform = other.gameObject.transform.GetChild(0);
            if (childTransform != null && childTransform.gameObject != null)
            {
                childTransform.gameObject.SetActive(true);
            }
        }
    }
}