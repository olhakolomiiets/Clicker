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
    private Transform variantsRoot;

    public List<ItemVariant> Variants => itemData != null ? itemData.Variants : null;

    public bool isActive;
    public bool isActiveVariant;
    public List<ItemVariantSaveData> state;

    [SerializeField] private int activeVariantId = -1;
    [SerializeField] private string variantsRootName = "VariantsRoot";

    public event Action<MetaVariantItemController> OnVariantPanelOpened;
    public event Action OnSelectVariantItem;

    private Vector3 mouseDownPosition;
    private bool isDragging;
    [SerializeField] private float dragThreshold = 15f;

    void Start()
    {
        if (itemData == null || itemData.Variants == null)
        {
            Debug.LogWarning($"[MetaVariantItemController] itemData/Variants missing on '{name}'");
            return;
        }

        EnsureVariantsRoot();

        if (state == null || state.Count == 0)
        {
            state = new List<ItemVariantSaveData>();
            foreach (var variant in itemData.Variants)
                state.Add(new ItemVariantSaveData { isBought = false, isActive = false });
        }

        if (!isActive)
        {
            if (box == null && itemData.BoxPrefab != null)
                box = Instantiate(itemData.BoxPrefab, transform);

            if (variantsRoot != null)
                variantsRoot.gameObject.SetActive(false);

            return;
        }

        BuildInstancesIfNeeded();
        ApplyVisualStateFromFlags();
    }

    private void EnsureVariantsRoot()
    {
        if (variantsRoot != null) return;

        var existing = transform.Find(variantsRootName);
        if (existing != null)
        {
            variantsRoot = existing;
            return;
        }

        var go = new GameObject(variantsRootName);
        variantsRoot = go.transform;
        variantsRoot.SetParent(transform, false);
        variantsRoot.SetSiblingIndex(transform.childCount - 1);
    }

    public void BuildInstancesIfNeeded()
    {
        if (!isActive) return;
        if (itemData?.Variants == null) return;

        EnsureVariantsRoot();

        if (instances != null && instances.Count == itemData.Variants.Count)
            return;

        instances ??= new List<GameObject>();

        foreach (var go in instances)
            if (go != null)
                Destroy(go);

        instances.Clear();

        if (variantsRoot == null) return;

        foreach (Transform child in variantsRoot)
            Destroy(child.gameObject);

        foreach (var v in itemData.Variants)
        {
            var go = Instantiate(v.Prefab, variantsRoot);
            go.SetActive(false);
            instances.Add(go);
        }
    }

    private void DisableAllInstances()
    {
        if (instances == null) return;

        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i] != null)
                instances[i].SetActive(false);
        }
    }

    private void ApplyVisualStateFromFlags()
    {
        if (!isActive)
        {
            if (box != null) box.SetActive(false);
            if (variantsRoot != null) variantsRoot.gameObject.SetActive(false);
            return;
        }

        EnsureVariantsRoot();
        BuildInstancesIfNeeded();

        if (!isActiveVariant)
        {
            if (box == null && itemData?.BoxPrefab != null)
                box = Instantiate(itemData.BoxPrefab, transform);

            if (box != null)
                box.SetActive(true);

            DisableAllInstances();

            if (variantsRoot != null)
                variantsRoot.gameObject.SetActive(false);

            activeVariantId = -1;
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

        if (activeVariantId < 0)
        {
            if (box == null && itemData?.BoxPrefab != null)
                box = Instantiate(itemData.BoxPrefab, transform);

            if (box != null)
                box.SetActive(true);

            DisableAllInstances();

            if (variantsRoot != null)
                variantsRoot.gameObject.SetActive(false);
        }
        else
        {
            if (box != null)
                box.SetActive(false);

            if (variantsRoot != null)
                variantsRoot.gameObject.SetActive(true);

            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i] != null)
                    instances[i].SetActive(i == activeVariantId);
            }
        }
    }

    public void SelectVariant(int index)
    {
        if (!IsVariantBought(index)) return;

        BuildInstancesIfNeeded();

        activeVariantId = index;
        isActiveVariant = true;

        if (box != null)
            box.SetActive(false);

        if (variantsRoot != null)
            variantsRoot.gameObject.SetActive(true);

        DisableAllInstances();

        if (activeVariantId >= 0 && activeVariantId < instances.Count && instances[activeVariantId] != null)
            instances[activeVariantId].SetActive(true);

        SetActiveVariant(index);
    }

    public void OnBuyVariant(int index)
    {
        if (index < 0 || state == null || index >= state.Count) return;

        BuildInstancesIfNeeded();

        activeVariantId = index;
        isActiveVariant = true;

        if (box != null)
            box.SetActive(false);

        if (variantsRoot != null)
            variantsRoot.gameObject.SetActive(true);

        DisableAllInstances();

        if (activeVariantId >= 0 && activeVariantId < instances.Count && instances[activeVariantId] != null)
            instances[activeVariantId].SetActive(true);

        BuyVariant(index);
        SetActiveVariant(index);
    }

    //void OnMouseDown()
    //{
    //    OnVariantPanelOpened?.Invoke(this);
    //}

    void OnMouseDown()
    {
        mouseDownPosition = Input.mousePosition;
        isDragging = false;
    }

    void OnMouseDrag()
    {
        if (Vector3.Distance(Input.mousePosition, mouseDownPosition) > dragThreshold)
            isDragging = true;
    }

    void OnMouseUpAsButton()
    {
        if (!isDragging)
            OnVariantPanelOpened?.Invoke(this);
    }

    #region SAVE / LOAD

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

        if (state == null)
            state = new List<ItemVariantSaveData>();

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

        isActiveVariant = state.Any(s => s.isActive);

        activeVariantId = -1;
        for (int i = 0; i < state.Count; i++)
        {
            if (state[i].isActive)
            {
                activeVariantId = i;
                break;
            }
        }

        BuildInstancesIfNeeded();
        ApplyVisualStateFromFlags();
    }

    public bool IsVariantBought(int index)
    {
        return state != null && index >= 0 && index < state.Count && state[index].isBought;
    }

    public bool IsVariantActive(int index)
    {
        return state != null && index >= 0 && index < state.Count && state[index].isActive;
    }

    public void BuyVariant(int index)
    {
        if (state == null || index < 0 || index >= state.Count) return;
        state[index].isBought = true;
    }

    public void SetActiveVariant(int index)
    {
        if (state == null || index < 0 || index >= state.Count) return;

        for (int i = 0; i < state.Count; i++)
            state[i].isActive = (i == index);

        isActiveVariant = true;
        OnSelectVariantItem?.Invoke();
    }

    #endregion
}