using UnityEngine;
using System.Collections.Generic;
using System;

public class MetaUpgradeItemController : MonoBehaviour
{
    [SerializeField] private string upgradeId;
    public MetaUpgradeItemData data;
    private GameObject instance;
    private GameObject box;
    // [SerializeField] private List<GameObject> activatesPerLevel;
    // [SerializeField] private List<GameObject> deactivatesPerLevel;

    [SerializeField] private UpgradePanelUI panelUI;
    public UpgradePanelUI PanelUI => panelUI;

    public MetaObjectController itemController;

    public List<PlanetObject> objectsToActivate;
    [SerializeField] private int itemCount = 0;
    [SerializeField] private ObjectPlaceRotator objectPlaceRotator;
    [HideInInspector] public int currentLevel;
    public bool isActive;

    private const string ES3_FILE = "meta_upgrades.es3";
    private string Es3Key => $"upgrade.{(string.IsNullOrEmpty(upgradeId) ? gameObject.name : upgradeId)}";

    private Vector3 mouseDownPosition;
    private bool isDragging;
    [SerializeField] private float dragThreshold = 15f;

    void Start()
    {
        ApplyLevel(currentLevel);
    }

    public void SetMetaObjectController(MetaObjectController item)
    {
        itemController = item;
        itemController.OnObjectAddButtonClicked += InstantiateBoxPrefab;
    }

    public bool CanUpgrade()
    {
        return currentLevel < data.Upgrades.Count - 1;
    }

    public void Upgrade()
    {
        if (!CanUpgrade()) return;
        var next = currentLevel + 1;
        var lvlData = data.Upgrades[currentLevel];

        currentLevel = next;
        ApplyLevel(currentLevel);
    }

    void ApplyLevel(int lvl)
    {
        Debug.Log($"[Upgrade] {gameObject.name}  level {lvl}, bonus = {data.Upgrades[lvl].BonusValue}");

        // for (int i = 0; i < activatesPerLevel.Count; i++)
        //     activatesPerLevel[i].SetActive(i <= lvl);
        // for (int i = 0; i < deactivatesPerLevel.Count; i++)
        //     deactivatesPerLevel[i].SetActive(i > lvl);

        if (lvl > 0)
            BuildInstance();
    }

    //void OnMouseDown()
    //{
    //    panelUI.Open(this);
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
            panelUI.Open(this);
    }

    private void InstantiateBoxPrefab()
    {
        if (!isActive)
        {
            if (box == null && data?.BoxPrefab != null)
            {
                box = Instantiate(data.BoxPrefab, transform);
                isActive = true;
            }
        }
    }

    public void BuildInstance()
    {
        if (isActive && currentLevel == 0)
        {
            if (box == null && data?.BoxPrefab != null)
                box = Instantiate(data.BoxPrefab, transform);

            return;         
        }

        if (currentLevel > 0)
        {
            if (box != null) box.SetActive(false);

            if (instance != null)
                instance.SetActive(true);
            else
                instance = Instantiate(data.BasePrefab, transform);   
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
        // Debug.Log($"[ES3 SAVE] {Es3Key} -> lvl={payload.upgradeLevel}, active={payload.isActive}");
    }

    public void LoadFromES3()
    {
        if (!ES3.KeyExists(Es3Key, ES3_FILE)) return;

        var payload = ES3.Load<ItemUpgradeSaveData>(Es3Key, ES3_FILE);
        LoadFromSave(payload);
        // Debug.Log($"[ES3 LOAD] {Es3Key} <- lvl={payload?.upgradeLevel}, active={payload?.isActive}");
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