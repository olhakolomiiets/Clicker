using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MetaVariantsController : MonoBehaviour
{
    public List<MetaVariantItemController> variantControllerList;

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
    [SerializeField] private int currentIndex = 0;
    [SerializeField] private ObjectPlaceRotator objectPlaceRotator;

    public event Action OnVariantOpened;
    public event Action<int> OnVariantBuyButonClicked, OnVariantObjectAddButtonClicked;

    void OnEnable()
    {
        foreach (MetaVariantItemController controller in variantControllerList)
        {
            PlanetObject obj = controller.GetComponent<PlanetObject>();
            objectsToActivate.Add(obj);

            controller.OnVariantPanelOpened += OpenVariantPanel;
        }

        _closeButton.onClick.AddListener(CloseVariantPanel);
    }

    public void OpenVariantPanel(MetaVariantItemController item)
    {
        _variant = item;
        _variantPanel.SetActive(true);

        for (int i = 0; i < item.Variants.Count; i++)
        {
            VariantButtonUI btn = Instantiate(_variantButton, _variantItemParent).GetComponent<VariantButtonUI>();

            _variantButtonList.Add(btn);

            btn.Init(item.Variants[i], i, item);
            ConnectVariantEvents(i, btn);

            Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! MetaPlanetUI /// InstantiateVariants /// Add to _variantButtonList" + btn);
        }

        OnVariantOpened?.Invoke();
    }

    public void CloseVariantPanel()
    {
        foreach (Transform t in _variantItemParent)
        {
            Debug.Log("🗑 Удаляется объект: " + t.name);
            Destroy(t.gameObject);
        }

        _variantButtonList.Clear();
    }

    public void UpdateVariantUI(int index, GeneralGameData data)
    {
        _variantButtonList[index].ToggleBuyButton(data.Diamonds >= data.VariantItemDataList[index].VariantItemPrice(data.VariantItemCount[index]));
    }

    private void ConnectVariantEvents(int i, VariantButtonUI variantButton)
    {
        variantButton.OnBuyButtonClicked += () => OnVariantBuyButonClicked?.Invoke(i);
    }

    private void ConnectEvents(int i, MetaObjectController itemController)
    {
        itemController.OnObjectAddButtonClicked += () => OnVariantObjectAddButtonClicked?.Invoke(i);
    }

    #region OBJECT ACTIVATOR

    public void ActivateNextObject()
    {
        if (currentIndex < objectsToActivate.Count)
        {
            objectPlaceRotator.AddToNeedToShowList(objectsToActivate[currentIndex]);
            currentIndex++;
        }
        else
        {
            currentIndex = 0;
        }

        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! ObjectActivator /// ActivateNextObject");
    }

    public void ActivatePurchasedObject(int itemCount)
    {
        for (int i = 0; i < itemCount; i++)
        {
            Animator animator = objectsToActivate[i].gameObject.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = false;
            }
            objectsToActivate[i].gameObject.SetActive(true);
        }

        currentIndex = itemCount;
    }
    #endregion

    private void OnDisable()
    {
        _closeButton.onClick.RemoveListener(CloseVariantPanel);
    }

}
