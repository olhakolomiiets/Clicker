using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class MetaVariantsController : MonoBehaviour
{
    public string controllerId;
    public List<MetaVariantItemController> items;

    [Header("Variant UI")]
    [SerializeField] private GameObject _variantButton;
    [SerializeField] private RectTransform _variantItemParent;
    [SerializeField] private GameObject _variantPanel;
    [SerializeField] private Button _closeButton;
    [SerializeField] private MetaVariantItemController _variant;
    public List<VariantButtonUI> _variantButtonList = new();

    [Header("Variant Object Activator")]
    public MetaObjectController itemController;
    public List<PlanetObject> objectsToActivate = new();
    [SerializeField] private int itemCount = 0;
    public int ItemCount => itemCount;
    [SerializeField] private MetaObjectPlaceRotator objectPlaceRotator;

    private GeneralGameData gameData;

    public event Action OnVariantOpened;
    public event Action<double> OnVariantBuyButonClicked, OnVariantObjectAddButtonClicked;

    #region ES3 SETTINGS
    private const string ES3_FILE = "meta.es3";
    private string Es3Key => $"meta.{controllerId}";
    #endregion

    void OnEnable()
    {
        objectsToActivate.Clear();

        foreach (MetaVariantItemController controller in items)
        {
            PlanetObject obj = controller.GetComponent<PlanetObject>();
            objectsToActivate.Add(obj);

            controller.OnVariantPanelOpened += OpenVariantPanel;
        }

        _closeButton.onClick.AddListener(CloseVariantPanel);

        LoadFromES3();
    }

    public void OpenVariantPanel(MetaVariantItemController item)
    {
        _variant = item;
        _variantPanel.SetActive(true);

        for (int i = 0; i < item.Variants.Count; i++)
        {
            VariantButtonUI btn = Instantiate(_variantButton, _variantItemParent).GetComponent<VariantButtonUI>();

            _variantButtonList.Add(btn);

            btn.Init(item.Variants[i], i, item, item.state[i].isActive, item.state[i].isBought);        

            if (gameData != null) btn.ToggleBuyButton(gameData.Diamonds >= item.Variants[i].Price);
            ConnectVariantEvents(btn);
        }
        _variant.OnSelectVariantItem += UpdateVariantUI;

        OnVariantOpened?.Invoke();
    }

    public void CloseVariantPanel()
    {
        foreach (Transform t in _variantItemParent)
            Destroy(t.gameObject);

        _variantButtonList.Clear();
    }

    public void PrepareData(GeneralGameData data)
    {
        gameData = data;
        UpdateVariantUI();
    }

    public void SetMetaObjectController(MetaObjectController item)
    {
        itemController = item;
        itemController.OnObjectAddButtonClicked += ActivateNextObject;
    }

    public void UpdateVariantUI()
    {
        if (gameData == null) return;

        foreach (var btn in _variantButtonList)
        {
            btn.ToggleBuyButton(gameData.Diamonds >= btn.Price);
            btn.UpdateButtonText(_variant.state[btn.Index].isActive, _variant.state[btn.Index].isBought);
        }
    }

    public void MetaObjectUI()
    {
        bool allActive = items.Any(item => !item.gameObject.activeInHierarchy);
        itemController.DisableBuyPanel(allActive);
    }

    private void ConnectVariantEvents(VariantButtonUI variantButton)
    {
        variantButton.OnBuyButtonClicked += (price) => OnVariantBuyButonClicked?.Invoke(price);
    }

    private void ConnectEvents(int i, MetaObjectController itemController)
    {
        itemController.OnObjectAddButtonClicked += () => OnVariantObjectAddButtonClicked?.Invoke(i);
    }

    #region  SAVE / LOAD

    public VariantsControllerSaveData GetSaveData()
    {
        VariantsControllerSaveData data = new();
        data.id = controllerId;
        data.variantCount = itemCount;

        foreach (var item in items)
            data.items.Add(item.GetSaveData());

        return data;
    }

    public void LoadFromSave(VariantsControllerSaveData data)
    {
        ValidateUniqueIds();

        itemCount = data.variantCount;

        foreach (var itemData in data.items)
        {
            var controller = items.FirstOrDefault(c => c.itemId == itemData.id);
            if (controller == null)
            {
                Debug.LogWarning($"[LOAD /// MetaVariantsController] No controller for id='{itemData.id}' in scene '{controllerId}'.");
                continue;
            }

            bool hasVariant = itemData.variants != null && itemData.variants.Any(v => v.isBought || v.isActive);
            bool shouldBeActive = itemData.isActive || hasVariant;

            controller.isActive = shouldBeActive;
            controller.gameObject.SetActive(shouldBeActive);

            itemData.isActive = shouldBeActive;

            controller.LoadFromSave(itemData);
        }

        Debug.Log($"[LOAD /// MetaVariantsController] ControllerId={controllerId}, Items loaded.");
    }

    public void SaveToES3()
    {
        var json = JsonUtility.ToJson(GetSaveData(), true);
        ES3.Save(Es3Key, json, ES3_FILE);
        Debug.Log($"[ES3 SAVE] {controllerId}\n{json}");
    }

    public void LoadFromES3()
    {
        if (!ES3.KeyExists(Es3Key, ES3_FILE)) return;
        var json = ES3.Load<string>(Es3Key, ES3_FILE);
        if (string.IsNullOrEmpty(json)) return;

        var data = JsonUtility.FromJson<VariantsControllerSaveData>(json);
        if (data == null || data.items == null || data.items.Count == 0) return;

        LoadFromSave(data);
    }
    private void OnApplicationPause(bool pause)
    {
        if (pause) SaveToES3();
    }
    private void OnDisable()
    {
        _closeButton.onClick.RemoveListener(CloseVariantPanel);
        SaveToES3();
    }
    private void OnDestroy()
    {
        SaveToES3();
    }

    #endregion

    #region OBJECT ACTIVATOR

    public void ActivateNextObject()
    {
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

    public void ActivatePurchasedObject(int objectCount)
    {
        for (int i = 0; i < objectCount; i++)
        {
            Animator animator = objectsToActivate[i].gameObject.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            objectsToActivate[i].gameObject.SetActive(true);
        }

        itemCount = objectCount;
    }
    #endregion

    private void ValidateUniqueIds()
    {
        var dups = items.GroupBy(i => i.itemId)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();
        if (dups.Count > 0)
            Debug.LogError($"[LOAD /// MetaVariantsController] Duplicate itemId(s) in '{controllerId}': {string.Join(", ", dups)}");
    }
}


#region SAVE DATA
[Serializable]
public class ItemVariantSaveData
{
    public bool isBought;
    public bool isActive;
}

[Serializable]
public class ItemControllerSaveData
{
    public string id;
    public bool isActive;
    public List<ItemVariantSaveData> variants = new();
}

[Serializable]
public class VariantsControllerSaveData
{
    public string id;
    public int variantCount;
    public List<ItemControllerSaveData> items = new();
}
#endregion