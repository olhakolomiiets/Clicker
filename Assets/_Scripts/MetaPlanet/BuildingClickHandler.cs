using UnityEngine;

public class BuildingClickHandler : MonoBehaviour
{
    public VariantPanel panel;
    private MetaVariantItemController controller;

    void Awake()
    {
        controller = GetComponent<MetaVariantItemController>();
        
    }
    void OnMouseDown()
    {
        //panel.OpenFor(controller);
    }
}
