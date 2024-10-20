using Lean.Localization;
using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ItemController : MonoBehaviour
{
    [SerializeField] private ProgressBar _progressBar;
    [SerializeField] private ProgressButon _progressButton;
    [SerializeField] private TextMeshProUGUI _itemTitle;
    [SerializeField] private TextMeshProUGUI _itemScore;
    [SerializeField] private Button _buyButton;
    [SerializeField] private TextMeshProUGUI _buyButtonText;
    [SerializeField] private TextMeshProUGUI _itemCount;
    [SerializeField] private Image _itemImage;
    [SerializeField] private GameObject _premiumImage;
    [SerializeField] private UIPurchaseInfo _purchaseInfo;
    [SerializeField] private TextMeshProUGUI _purchaseInfoText;
    [SerializeField] private GameObject _buyPanel;
    [SerializeField] private GameObject _managerButton;
    private bool _isPremium;
    private bool isAuto;
    private int _maxItems;
    private string _translationText;
    public event Action OnProgressButtonClicked, OnWorkFinished, OnPremiumItemWorkFinished, OnBuyButtonClicked, OnActivationPremium, OnFirstActivation;
    public bool isWorking => _progressButton.IsEnabled == false;
    private void Awake()
    {
        _progressButton.OnButtonClicked.AddListener(HandleFirstClick);
        _progressBar.OnProgressBarFinished.AddListener(HandleProgressBarFinished);
        _buyButton.onClick.AddListener(HandleBuyButton);
        ToggleBuyButton(false);
        _progressButton.IsEnabled = false;
        ToggleIncome(false);
    }

    void Start()
    {
        if (_progressButton.IsEnabled && isAuto)
            OnProgressButtonClicked?.Invoke();
    }

    public void Prepare(Sprite icon, bool isPremium, string translationText, int maxItemsCount, bool auto)
    {
        _translationText = translationText;
        _itemImage.sprite = icon;
        _isPremium = isPremium;
        _maxItems = maxItemsCount;
        isAuto = auto;

        if (_isPremium)
            _premiumImage.SetActive(true);

        if (_purchaseInfo.isActiveAndEnabled)
        {
            _purchaseInfoText.text = $"{LeanLocalization.GetTranslationText("Unlock")} {LeanLocalization.GetTranslationText(_translationText)}";
        }
    }

    public void ActivateButton()
    {
        ToggleIncome(true);
        _purchaseInfo.gameObject.SetActive(false);
        _progressBar.gameObject.SetActive(true);
        _progressButton.IsEnabled = true;
        _itemTitle.text = LeanLocalization.GetTranslationText(_translationText);
        if (_maxItems > 1)
        {
            _buyPanel.SetActive(true);
            if (!_isPremium)
                _managerButton.SetActive(true);
        }
        _progressButton.OnButtonClicked.RemoveAllListeners();
        _progressButton.OnButtonClicked.AddListener(HandleProgressButtonClick);
    }

    internal void ResetEvents()
    {
        OnProgressButtonClicked = null;
        OnWorkFinished = null;
        OnBuyButtonClicked = null;
        OnFirstActivation = null;
    }

    public void ToggleActivation(bool val)
    {
        if (val)
        {
            _progressButton.IsEnabled = true;
            _purchaseInfo.SwapImageReady();
        }
        else
        {
            _progressButton.IsEnabled = false;
            _purchaseInfo.SwapImageNotReady();
        }
    }

    public void AutomateButton()
    {
        _progressButton.IsEnabled = false;
        OnWorkFinished = null;
        _progressBar.ResetProgress();
    }

    private void HandleFirstClick()
    {
        OnFirstActivation?.Invoke();

        if (_isPremium)
            OnActivationPremium?.Invoke();

        ActivateButton();
    }

    private void HandleProgressButtonClick()
    {
        OnProgressButtonClicked?.Invoke();
    }

    private void HandleBuyButton()
        => OnBuyButtonClicked?.Invoke();

    public void SetBuyPrice(double price)
    {
        if (_purchaseInfo.isActiveAndEnabled)
        {
            if (price > 1000)
                _purchaseInfo.SetPrice(AbbreviateNumber(price));
            else
                _purchaseInfo.SetPrice($"{price.ToString("N0")}");

            return;
        }
        if (price > 1000)
            _buyButtonText.text = AbbreviateNumber(price);
        else
            _buyButtonText.text = $"{price.ToString("N0")}";
    }

    public void SetItemCount(int count, int maxCount)
        => _itemCount.text = $"{count} / {maxCount}";

    public void ToggleBuyButton(bool val)
        => _buyButton.interactable = val;

    public void StartWork(float delay)
    {
        _progressButton.IsEnabled = false;
        _progressBar.RunProgressBar(delay);
    }

    public void ToggleIncome(bool val)
        => _itemScore.gameObject.SetActive(val);

    public void SetIncome(double score, string sec)
    {
        if (score > 1000)
            _itemScore.text = $"{AbbreviateNumber(score) + sec}";
        else
            _itemScore.text = $"{score.ToString("N0") + sec}";
    }

    private void HandleProgressBarFinished()
    {
        _progressButton.IsEnabled = true;
        OnWorkFinished?.Invoke();
    }

    string AbbreviateNumber(double number)
    {
        string[] suffixes = { "", "K", "M", "B", "T" };
        int suffixIndex = 0;
        while (number >= 1000 && suffixIndex < suffixes.Length - 1)
        {
            number /= 1000;
            suffixIndex++;
        }
        return string.Format("{0:0.##} {1}", number, suffixes[suffixIndex]).Replace(',', '.');
    }

    public void UpdateLanguage()
    {
        if (_purchaseInfo.isActiveAndEnabled)
        {
            _purchaseInfoText.text = $"{LeanLocalization.GetTranslationText("Unlock")} {LeanLocalization.GetTranslationText(_translationText)}";
        }
        _itemTitle.text = LeanLocalization.GetTranslationText(_translationText);
    }

    private void OnDisable()
    {
        _progressButton.OnButtonClicked.RemoveListener(HandleProgressButtonClick);
        _progressBar.OnProgressBarFinished.RemoveListener(HandleProgressBarFinished);
        _buyButton.onClick.RemoveListener(HandleBuyButton);
    }

}
