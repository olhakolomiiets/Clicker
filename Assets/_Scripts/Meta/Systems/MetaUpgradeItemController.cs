using UnityEngine;
using System.Collections.Generic;
using System;

public class MetaUpgradeItemController : MonoBehaviour
{
    public enum UpgradeVisualMode
    {
        BoxThenPrefab,
        BoxWithChildrenByLevel
    }

    [SerializeField] private string upgradeId;
    public MetaUpgradeItemData data;
    private GameObject instance;
    private GameObject box;

    [SerializeField] private UpgradePanelUI panelUI;
    public UpgradePanelUI PanelUI => panelUI;

    public MetaObjectController itemController;

    public List<PlanetObject> objectsToActivate;
    [SerializeField] private int itemCount = 0;
    [SerializeField] private MetaObjectPlaceRotator objectPlaceRotator;
    [HideInInspector] public int currentLevel;
    public bool isActive;

    [Header("Visual Mode")]
    [SerializeField] private UpgradeVisualMode visualMode = UpgradeVisualMode.BoxThenPrefab;

    [Header("Box Children By Level Mode")]
    [SerializeField] private int activeChildrenOnPurchase = 2;

    private const string ES3_FILE = "meta_upgrades.es3";
    private string Es3Key => $"upgrade.{(string.IsNullOrEmpty(upgradeId) ? gameObject.name : upgradeId)}";

    private Vector3 mouseDownPosition;
    private bool isDragging;
    [SerializeField] private float dragThreshold = 15f;

    void Awake()
    {
        LoadFromES3();
    }

    void Start()
    {
        ApplyLevel(currentLevel);
        BuildInstance();
    }

    public void SetMetaObjectController(MetaObjectController item)
    {
        itemController = item;
        itemController.OnObjectAddButtonClicked += InstantiateBoxPrefab;
    }

    public bool CanUpgrade()
    {
        return data != null &&
               data.Upgrades != null &&
               data.Upgrades.Count > 0 &&
               currentLevel < data.Upgrades.Count - 1;
    }

    public void Upgrade()
    {
        if (!CanUpgrade()) return;

        currentLevel++;
        ApplyLevel(currentLevel);
        SaveToES3();
    }

    void ApplyLevel(int lvl)
    {
        if (data != null && data.Upgrades != null && lvl >= 0 && lvl < data.Upgrades.Count)
            Debug.Log($"[Upgrade] {gameObject.name} level {lvl}, bonus = {data.Upgrades[lvl].BonusValue}");

        if (!isActive)
        {
            HideAllVisuals();
            return;
        }

        if (visualMode == UpgradeVisualMode.BoxWithChildrenByLevel)
        {
            EnsureBoxExists();
            ApplyBoxChildrenState();
            return;
        }

        if (lvl > 0)
            BuildInstance();
    }

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
            panelUI.Open(this);
    }

    private void InstantiateBoxPrefab()
    {
        if (isActive) return;

        isActive = true;

        EnsureBoxExists();

        if (box != null)
            box.SetActive(true);

        if (visualMode == UpgradeVisualMode.BoxWithChildrenByLevel)
            ApplyBoxChildrenState();

        SaveToES3();
    }

    public void BuildInstance()
    {
        if (!isActive)
        {
            HideAllVisuals();
            return;
        }

        if (visualMode == UpgradeVisualMode.BoxWithChildrenByLevel)
        {
            EnsureBoxExists();
            ApplyBoxChildrenState();
            return;
        }

        if (isActive && currentLevel == 0)
        {
            if (instance != null)
                instance.SetActive(false);

            EnsureBoxExists();

            if (box != null)
                box.SetActive(true);

            return;
        }

        if (currentLevel > 0)
        {
            if (box != null)
                box.SetActive(false);

            var targetPrefab = GetPrefabForCurrentLevel();
            if (targetPrefab == null)
                return;

            if (instance != null)
                Destroy(instance);

            instance = Instantiate(targetPrefab, transform);
            instance.SetActive(true);
        }
    }

    private void EnsureBoxExists()
    {
        if (box == null && data?.BoxPrefab != null)
            box = Instantiate(data.BoxPrefab, transform);
    }

    private void HideAllVisuals()
    {
        if (box != null)
            box.SetActive(false);

        if (instance != null)
            instance.SetActive(false);

        DisableAllBoxChildren();
    }

    private void DisableAllBoxChildren()
    {
        if (box == null) return;

        for (int i = 0; i < box.transform.childCount; i++)
        {
            var child = box.transform.GetChild(i);
            if (child != null)
                child.gameObject.SetActive(false);
        }
    }

    private void ApplyBoxChildrenState()
    {
        if (box == null) return;

        if (instance != null)
            instance.SetActive(false);

        box.SetActive(true);
        DisableAllBoxChildren();

        int activeCount = Mathf.Clamp(activeChildrenOnPurchase + currentLevel, 0, box.transform.childCount);

        for (int i = 0; i < activeCount; i++)
        {
            var child = box.transform.GetChild(i);
            if (child != null)
                child.gameObject.SetActive(true);
        }
    }

    public void ActivateUpgradeObject()
    {
        if (objectsToActivate == null || objectPlaceRotator == null) return;

        if (itemCount < objectsToActivate.Count)
        {
            objectPlaceRotator.AddToNeedToShowList(objectsToActivate[itemCount]);
            itemCount++;
        }
        else
        {
            itemCount = 0;
        }
    }

    private GameObject GetPrefabForCurrentLevel()
    {
        if (data == null || data.LevelPrefabs == null || data.LevelPrefabs.Count == 0)
            return null;

        int prefabIndex = currentLevel - 1;

        if (prefabIndex < 0 || prefabIndex >= data.LevelPrefabs.Count)
            return null;

        return data.LevelPrefabs[prefabIndex];
    }


    #region SAVE / LOAD (Easy Save 3)

    public ItemUpgradeSaveData GetSaveData()
    {
        return new ItemUpgradeSaveData
        {
            upgradeLevel = currentLevel,
            isActive = isActive
        };
    }

    public void LoadFromSave(ItemUpgradeSaveData s)
    {
        if (s == null) return;

        currentLevel = s.upgradeLevel;
        isActive = s.isActive;

        ApplyLevel(currentLevel);
        BuildInstance();
    }

    public void SaveToES3()
    {
        var payload = GetSaveData();
        ES3.Save(Es3Key, payload, ES3_FILE);
    }

    public void LoadFromES3()
    {
        if (!ES3.KeyExists(Es3Key, ES3_FILE)) return;

        var payload = ES3.Load<ItemUpgradeSaveData>(Es3Key, ES3_FILE);
        LoadFromSave(payload);
    }

    #endregion
}

#region SAVE DATA
[Serializable]
public class ItemUpgradeSaveData
{
    public int upgradeLevel;
    public bool isActive;
}
#endregion