using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MetaVariantItemController : MonoBehaviour
{
    public string itemId;
    public MetaVariantItemData itemData;
    private List<GameObject> instances = new List<GameObject>();
    private GameObject box;
    public List<ItemVariant> Variants => itemData != null ? itemData.Variants : null;

    public bool isActive;
    public bool isActiveVariant;
    public List<ItemVariantSaveData> state;
    [SerializeField] private int activeVariantId;
    public event Action<MetaVariantItemController> OnVariantPanelOpened;
    public event Action OnSelectVariantItem;

    void Start()
    {
        state = new List<ItemVariantSaveData>();
        foreach (var variant in itemData.Variants)
            state.Add(new ItemVariantSaveData { isBought = false, isActive = false });

        if (!isActive)
        {
            if (box == null && itemData?.BoxPrefab != null)
            {
                box = Instantiate(itemData.BoxPrefab, transform);
                isActive = true;
                Debug.LogWarning($"[Start /// MetaVariantItemController /// isActive: ] '{isActive}'");
            }
            return;
        }
    }

    public void BuildInstancesIfNeeded()
    {
        if (!isActive) return;

        if (itemData?.Variants == null) return;
        if (instances != null && instances.Count == itemData.Variants.Count) return;

        instances ??= new List<GameObject>();
        foreach (var go in instances) if (go != null) Destroy(go);
        instances.Clear();

        foreach (var v in itemData.Variants)
        {
            var go = Instantiate(v.Prefab, transform);
            go.SetActive(false);
            instances.Add(go);
        }
    }

    private void ApplyVisualStateFromFlags()
    {
        if (!isActiveVariant && isActive)
        {
            if (box == null && itemData?.BoxPrefab != null)
            {
                box = Instantiate(itemData.BoxPrefab, transform);
            }
            return;
        }

        activeVariantId = -1;
        for (int i = 0; i < state.Count; i++)
        {
            if (state[i].isActive)
            {
                activeVariantId = i;
                break;
            }
        }

        if (activeVariantId < 0 && isActive)
        {
            if (box == null && itemData?.BoxPrefab != null)
                box = Instantiate(itemData.BoxPrefab, transform);
            if (box != null) box.SetActive(true);
            foreach (var inst in instances) if (inst != null) inst.SetActive(false);
        }
        else if (activeVariantId >= 0 && isActive)
        {
            if (box != null) box.SetActive(false);
            for (int i = 0; i < instances.Count; i++)
                if (instances[i] != null) instances[i].SetActive(i == activeVariantId);
        }
    }

    public void SelectVariant(int index)
    {
        if (!IsVariantBought(index)) return;
        BuildInstancesIfNeeded();
        if (activeVariantId >= 0 && activeVariantId < instances.Count && instances[activeVariantId] != null)
            instances[activeVariantId].SetActive(false);

        activeVariantId = index;
        if (activeVariantId >= 0 && activeVariantId < instances.Count && instances[activeVariantId] != null)
            instances[activeVariantId].SetActive(true);

        SetActiveVariant(index);
    }

    public void OnBuyVariant(int index)
    {
        BuildInstancesIfNeeded();

        if (box != null && box.activeSelf) box.SetActive(false);
        else if (activeVariantId >= 0 && activeVariantId < instances.Count && instances[activeVariantId] != null)
            instances[activeVariantId].SetActive(false);

        activeVariantId = index;
        if (activeVariantId >= 0 && activeVariantId < instances.Count && instances[activeVariantId] != null)
        {
            instances[activeVariantId].SetActive(true);
            isActiveVariant = true;
        }

        BuyVariant(index);
        SetActiveVariant(index);
    }

    void OnMouseDown()
    {
        OnVariantPanelOpened?.Invoke(this);
    }

    #region  SAVE / LOAD (данные одного Item)

    public ItemControllerSaveData GetSaveData()
    {
        ItemControllerSaveData data = new();
        data.id = itemId;
        data.isActive = isActive;
        data.variants = state;
        return data;
    }

    public void LoadFromSave(ItemControllerSaveData data)
    {
        if (itemData == null || itemData.Variants == null)
        {
            Debug.LogWarning($"[LOAD /// MetaVariantItemController] '{name}' no itemData/Variants, skip.");
            return;
        }

        isActive = data.isActive;

        if (state == null) state = new List<ItemVariantSaveData>();
        if (state.Count != itemData.Variants.Count)
        {
            state.Clear();
            for (int i = 0; i < itemData.Variants.Count; i++)
                state.Add(new ItemVariantSaveData());
        }

        for (int i = 0; i < data.variants.Count && i < state.Count; i++)
        {
            state[i].isBought = data.variants[i].isBought;
            state[i].isActive = data.variants[i].isActive;
        }

        isActiveVariant = state.Any(s => s.isBought || s.isActive);

        activeVariantId = -1;
        for (int i = 0; i < state.Count; i++)
            if (state[i].isActive) { activeVariantId = i; break; }

        BuildInstancesIfNeeded();
        ApplyVisualStateFromFlags();
    }

    public bool IsVariantBought(int index) => state[index].isBought;
    public bool IsVariantActive(int index) => state[index].isActive;
    public void BuyVariant(int index) => state[index].isBought = true;

    public void SetActiveVariant(int index)
    {
        for (int i = 0; i < state.Count; i++)
        {
            state[i].isActive = (i == index);
        }    
        OnSelectVariantItem?.Invoke();
    }

    #endregion
}