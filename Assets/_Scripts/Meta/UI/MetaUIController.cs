using DG.Tweening;
using Exoa.Cameras;
using System;
using System.Collections;
using System.Net.NetworkInformation;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using PlanetBuilder.Messages;
using TMPro;
using System.Collections.Generic;

public class MetaUIController : MonoBehaviour
{
    [SerializeField] private GameObject _backgroundImage;

    [Header("Shop Panel")]
    [SerializeField] private RectTransform _toggleButton;
    [SerializeField] private RectTransform _shopPanel;
    [SerializeField] private float _panelTopPosY, _panelMiddlePosY;
    [SerializeField] private float _tweenDuration;

    [Header("Creation")]
    [SerializeField] private RectTransform _variantItemsParent;
    [SerializeField] private Button _variantButton;
    public GameObject ToggleButtonGameObject
    {
        get
        {
            if (_toggleButton == null)
                return null;

            Button toggleButton = _toggleButton.GetComponent<Button>();

            if (toggleButton == null)
                toggleButton = _toggleButton.GetComponentInChildren<Button>(true);

            return toggleButton != null ? toggleButton.gameObject : _toggleButton.gameObject;
        }
    }
    public GameObject VariantTabButtonGameObject => _variantButton != null ? _variantButton.gameObject : null;
    public GameObject UpgradeTabButtonGameObject => _upgradeButton != null ? _upgradeButton.gameObject : null;

    [Header("Upgrade")]
    [SerializeField] private RectTransform _upgradeItemsParent;
    [SerializeField] private Button _upgradeButton;

    [Header("Shop")]
    [SerializeField] private RectTransform _shopItemsParent;
    [SerializeField] private Button _shopButton;

    [Header("Planet Levels")]
    [SerializeField] private RectTransform _menuArrow;
    [SerializeField] private GameObject _bg;
    [SerializeField] private RectTransform _levelsPanel;
    [SerializeField] private float _levelsPanelTopPosX, _levelsPanelMiddlePosX;

    [HideInInspector] public bool isDisplayed;
    [HideInInspector] public bool isLevelsDisplayed;

    [SerializeField] private GameObject _planet;

    [Header("Planet Zoom")]
    [SerializeField] private DragRotateGPT _planetRotator;
    [SerializeField] private float zoomDuration;

    private MetaObjectPlaceRotator _objectPlaceRotator;
    private int _currentShopTabIndex = -1;

    [Header("Currency Exchange")]
    [SerializeField] private MetaPlanetMode _exchangeMode;
    [SerializeField] private GameObject _exchangePanel;
    [SerializeField] private TMP_Dropdown _exchangePlanetDropdown;
    [SerializeField] private TMP_Text _exchangePlanetBalanceText;
    [SerializeField] private TMP_Text _exchangeDiamondPriceText;
    [SerializeField] private TMP_InputField _exchangeAmountInput;
    [SerializeField] private TMP_Text _exchangeTotalCostText;
    [SerializeField] private TMP_Text _exchangeStatusText;
    [SerializeField] private Button _exchangeButton;
    [Header("Exchange Currency Icons")]
    [SerializeField] private MetaPlanetCurrencyExchange _exchangeCurrencyController;
    [SerializeField] private Image _exchangeDiamondPriceCurrencyIcon;
    [SerializeField] private Image _exchangeTotalCostCurrencyIcon;
    [SerializeField] private Image _exchangePlanetBalanceCurrencyIcon;

    private readonly List<int> _exchangePlanetIds = new();
    private bool _exchangeRotationWasEnabled;


    public bool IsUpgradeTabOpen => isDisplayed && _currentShopTabIndex == 1;

    public event Action OnStorePanelDisplayed;

    private void Start()
    {
        _planetRotator = _planet.GetComponent<DragRotateGPT>();
        _objectPlaceRotator = _planet.GetComponent<MetaObjectPlaceRotator>();
    }

    private void OnDisable()
    {
        CloseExchangePanel();
        SetShopOpen(false);
    }

    public void OpenShop()
    {
        if (_exchangePanel != null && _exchangePanel.activeSelf)
            return;

        if (isDisplayed)
        {
            ShopsToggle(2);
        }
        else
        {
            _planetRotator.SaveAndZoomToMax(zoomDuration);

            if (_backgroundImage != null)
            {
                StartCoroutine(SmoothScaleBackground(0.93f));
            }

            _shopPanel.DOAnchorPosY(_panelTopPosY, _tweenDuration);
            _toggleButton.DORotate(new Vector3(0, 0, 180), _tweenDuration);
            isDisplayed = true;
            SetShopOpen(true);
            _planetRotator.enabled = false;
            ShopsToggle(2);
        }
    }
    public void ToggleUpgradeStorePanel()
    {
        if (_exchangePanel != null && _exchangePanel.activeSelf)
            return;

        if (isDisplayed)
        {
            _planetRotator.ZoomBack(zoomDuration);

            if (_backgroundImage != null)
            {
                StartCoroutine(SmoothScaleBackground(1f));
            }

            _shopPanel.DOAnchorPosY(_panelMiddlePosY, _tweenDuration);
            _toggleButton.DORotate(new Vector3(0, 0, 0), _tweenDuration);
            isDisplayed = false;
            SetShopOpen(false);
            SetStartPos();
            if (!_objectPlaceRotator.isRotating)
            {
                _planetRotator.enabled = true;
            }

        }
        else
        {
            _planetRotator.SaveAndZoomToMax(zoomDuration);

            if (_backgroundImage != null)
            {
                StartCoroutine(SmoothScaleBackground(0.93f));
            }

            _variantItemsParent.DOAnchorPos(new Vector3(0, -350, 0), 0.25f);
            _variantButton.Select();

            _shopPanel.DOAnchorPosY(_panelTopPosY, _tweenDuration);
            _toggleButton.DORotate(new Vector3(0, 0, 180), _tweenDuration);
            isDisplayed = true;
            SetShopOpen(true);
            ColorToggle(0);
            _planetRotator.enabled = false;
        }
    }

    public void ToggleLevelsPanel()
    {
        if (_exchangePanel != null && _exchangePanel.activeSelf)
            return;

        if (isLevelsDisplayed)
        {
            _bg.SetActive(false);

            if (_backgroundImage != null)
            {
                StartCoroutine(SmoothScaleBackground(1f));
            }

            _levelsPanel.DOAnchorPosX(_levelsPanelMiddlePosX, _tweenDuration);
            _menuArrow.DORotate(new Vector3(0, 0, 0), _tweenDuration);
            isLevelsDisplayed = false;

            if (!_objectPlaceRotator.isRotating)
            {
                _planetRotator.enabled = true;
            }
        }
        else
        {
            _bg.SetActive(true);

            if (_backgroundImage != null)
            {
                StartCoroutine(SmoothScaleBackground(0.93f));
            }

            _levelsPanel.DOAnchorPosX(_levelsPanelTopPosX, _tweenDuration);

            _menuArrow.DORotate(new Vector3(0, 0, 180), _tweenDuration);
            isLevelsDisplayed = true;
            _planetRotator.enabled = false;
        }
    }

    IEnumerator SmoothScaleBackground(float scale)
    {
        Vector3 initialScale = _backgroundImage.transform.localScale;
        Vector3 targetScale = new Vector3(scale, scale, scale);

        float elapsedTime = 0f;

        while (elapsedTime < 0.8f)
        {
            elapsedTime += Time.deltaTime;
            _backgroundImage.transform.localScale = Vector3.Lerp(initialScale, targetScale, elapsedTime / 0.8f);
            yield return null;
        }
        _backgroundImage.transform.localScale = targetScale;
    }

    public void ShopsToggle(int index)
    {
        Vector3 visiblePos = new Vector3(0, -350, 0);
        Vector3 hidePos = new Vector3(0, -1130, 0);

        _currentShopTabIndex = index;
        _variantItemsParent.DOAnchorPos(index == 0 ? visiblePos : hidePos, 0.25f);
        _upgradeItemsParent.DOAnchorPos(index == 1 ? visiblePos : hidePos, 0.25f);
        _shopItemsParent.DOAnchorPos(index == 2 ? visiblePos : hidePos, 0.25f);

        ColorToggle(index);
    }

    private void ColorToggle(int i)
    {
        ColorBlock variantColors = _variantButton.colors;
        ColorBlock upgradeColors = _upgradeButton.colors;
        ColorBlock shopColors = _shopButton.colors;

        variantColors.normalColor = (i == 0) ? new Color(0.12f, 0.45f, 1f, 0.5f) : Color.white;
        upgradeColors.normalColor = (i == 1) ? new Color(0.12f, 0.45f, 1f, 0.5f) : Color.white;
        shopColors.normalColor = (i == 2) ? new Color(0.12f, 0.45f, 1f, 0.5f) : Color.white;

        _variantButton.colors = variantColors;
        _upgradeButton.colors = upgradeColors;
        _shopButton.colors = shopColors;
    }
    private void SetStartPos()
    {
        _currentShopTabIndex = -1;
        _variantItemsParent.DOAnchorPos(new Vector3(0, -1130, 0), 0.25f);
        _upgradeItemsParent.DOAnchorPos(new Vector3(0, -1130, 0), 0.25f);
        _shopItemsParent.DOAnchorPos(new Vector3(0, -1130, 0), 0.25f);
    }


    public void OpenExchangePanel()
    {
        if (_exchangePanel == null || _exchangeMode == null ||
            _exchangePlanetDropdown == null || _exchangeAmountInput == null ||
            _exchangeButton == null || _exchangePlanetBalanceText == null ||
            _exchangeDiamondPriceText == null || _exchangeTotalCostText == null ||
            _exchangeStatusText == null)
        {
            Debug.LogError("[ExchangeUI] Missing UI references.", this);
            return;
        }

        if (_exchangePanel.activeSelf || isDisplayed || isLevelsDisplayed)
            return;

        _exchangePlanetIds.Clear();
        _exchangePlanetIds.AddRange(_exchangeMode.OpenCurrencyExchange());

        var options = new List<TMP_Dropdown.OptionData>();

        foreach (int planetId in _exchangePlanetIds)
            options.Add(new TMP_Dropdown.OptionData($"Planet {planetId}"));

        if (options.Count == 0)
            options.Add(new TMP_Dropdown.OptionData("No available planets"));

        _exchangePlanetDropdown.ClearOptions();
        _exchangePlanetDropdown.AddOptions(options);
        _exchangePlanetDropdown.SetValueWithoutNotify(0);
        _exchangePlanetDropdown.RefreshShownValue();
        _exchangePlanetDropdown.interactable = _exchangePlanetIds.Count > 0;

        _exchangeAmountInput.SetTextWithoutNotify("1");

        _exchangeRotationWasEnabled =
            _planetRotator != null && _planetRotator.enabled;

        if (_planetRotator != null)
            _planetRotator.enabled = false;

        _exchangePanel.SetActive(true);
        SetShopOpen(true);

        RefreshExchangePreview();
    }

    public void CloseExchangePanel()
    {
        if (_exchangePanel == null || !_exchangePanel.activeSelf)
            return;

        _exchangeMode?.CloseCurrencyExchange();
        _exchangePanel.SetActive(false);
        _exchangePlanetIds.Clear();

        SetShopOpen(false);

        if (_exchangeRotationWasEnabled &&
            _planetRotator != null &&
            !isDisplayed && !isLevelsDisplayed &&
            (_objectPlaceRotator == null || !_objectPlaceRotator.isRotating))
        {
            _planetRotator.enabled = true;
        }
    }

    public void SelectExchangePlanet(int index)
    {
        RefreshExchangePreview();
    }

    public void ChangeExchangeAmount(string value)
    {
        RefreshExchangePreview();
    }

    private void UpdateExchangeCurrencyIcons(int planetId)
    {
        Sprite icon = _exchangeCurrencyController != null
            ? _exchangeCurrencyController.GetCurrencyIcon(planetId)
            : null;

        if (_exchangeDiamondPriceCurrencyIcon != null)
        {
            _exchangeDiamondPriceCurrencyIcon.sprite = icon;
            _exchangeDiamondPriceCurrencyIcon.enabled = icon != null;
        }

        if (_exchangeTotalCostCurrencyIcon != null)
        {
            _exchangeTotalCostCurrencyIcon.sprite = icon;
            _exchangeTotalCostCurrencyIcon.enabled = icon != null;
        }

        if (_exchangePlanetBalanceCurrencyIcon != null)
        {
            _exchangePlanetBalanceCurrencyIcon.sprite = icon;
            _exchangePlanetBalanceCurrencyIcon.enabled = icon != null;
        }
    }
    private void RefreshExchangePreview()
    {
        UpdateExchangeCurrencyIcons(-1);
        _exchangeButton.interactable = false;
        _exchangePlanetBalanceText.text = "—";
        _exchangeDiamondPriceText.text = "—";
        _exchangeTotalCostText.text = "—";
        _exchangeStatusText.text = "";

        if (_exchangePlanetIds.Count == 0)
        {
            _exchangeStatusText.text = "No available planets.";
            return;
        }

        int index = _exchangePlanetDropdown.value;

        if (index < 0 || index >= _exchangePlanetIds.Count)
            return;

        int planetId = _exchangePlanetIds[index];

        if (!_exchangeMode.TryGetCurrencyExchangeQuote(planetId, out MetaPlanetCurrencyExchange.ExchangeQuote quote, out double planetMoney, out string error))
        {
            _exchangeStatusText.text = error;
            return;
        }

        UpdateExchangeCurrencyIcons(planetId);

        _exchangePlanetBalanceText.text = planetMoney.ToString("N0");
        _exchangeDiamondPriceText.text = quote.pricePerDiamond.ToString("N0");

        if (!int.TryParse(_exchangeAmountInput.text, out int diamonds) || diamonds <= 0)
        {
            _exchangeStatusText.text = "Enter a valid diamond amount.";
            return;
        }

        double cost = Math.Ceiling(quote.pricePerDiamond * diamonds);

        if (double.IsNaN(cost) || double.IsInfinity(cost) || cost <= 0)
        {
            _exchangeStatusText.text = "Invalid exchange cost.";
            return;
        }

        _exchangeTotalCostText.text = cost.ToString("N0");
        _exchangeButton.interactable = planetMoney >= cost;

        if (!_exchangeButton.interactable)
            _exchangeStatusText.text = "Not enough planet currency.";
    }

    public void ConfirmExchange()
    {
        if (_exchangePanel == null || !_exchangePanel.activeSelf || !_exchangeButton.interactable || _exchangePlanetIds.Count == 0)
            return;

        if (!int.TryParse(_exchangeAmountInput.text, out int diamonds) || diamonds <= 0)
            return;

        int index = _exchangePlanetDropdown.value;

        if (index < 0 || index >= _exchangePlanetIds.Count)
            return;

        int planetId = _exchangePlanetIds[index];

        if (!_exchangeMode.TryGetCurrencyExchangeQuote(planetId, out MetaPlanetCurrencyExchange.ExchangeQuote quote, out _, out string error))
        {
            _exchangeStatusText.text = error;
            return;
        }

        var result = _exchangeMode.TryExchangeCurrency(quote, diamonds, out _, out error);

        RefreshExchangePreview();

        _exchangeStatusText.text =
            result == MetaPlanetCurrencyExchange.ExchangeResult.Success
                ? "Exchange successful."
                : error ?? result.ToString();
    }


    private static void SetShopOpen(bool isOpen)
    {
        MessageManager messageManager = MessageManager.Instance;

        if (messageManager != null)
            messageManager.SetShopOpen(isOpen);
    }
}
