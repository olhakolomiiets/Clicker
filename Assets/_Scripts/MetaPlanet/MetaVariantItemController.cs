using System;
using System.Collections.Generic;
using UnityEngine;

public class MetaVariantItemController : MonoBehaviour
{
    [SerializeField] private MetaVariantItemData data;
    private List<GameObject> instances = new List<GameObject>();
    public List<ItemVariant> Variants => data != null ? data.Variants : null;
    [SerializeField] private int currentIndex = 0;
    public event Action<MetaVariantItemController> OnVariantPanelOpened;

    void Start()
    {
        var box = Instantiate(data.BoxPrefab, transform);
        
        foreach (var v in Variants)
        {
            var go = Instantiate(v.Prefab, transform);
            go.SetActive(false);
            instances.Add(go);
        }
        
        //instances[currentIndex].SetActive(true);
    }

    public void PrepareItemData(int index)
    {
        currentIndex = index;
    }

    public void SelectVariant(int index)
    {
        instances[currentIndex].SetActive(false);
        currentIndex = index;
        instances[currentIndex].SetActive(true);
    }

    void OnMouseDown()
    {
        OnVariantPanelOpened?.Invoke(this);
    }
}
