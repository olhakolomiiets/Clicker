using DG.Tweening;
using Lean.Localization;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelController : MonoBehaviour
{
    [SerializeField] private List<Button> _buyButtons;
    [SerializeField] private List<Button> _levelButtons;   
    [SerializeField] private Color disabledColor = new Color(1, 1, 1, 0.5f);
    [SerializeField] private Color enabledColor = Color.white;

    [SerializeField] private GameObject levelInfo;

    private CanvasGroup tipCanvasGroup;
    private RectTransform tipRectTransform;
    private Vector2 originalPosition;
    private float duration = 0.5f;
    private bool isHidingTip = false;

    [SerializeField] private double nextPlanetPrice;

    private GeneralGameData generalData;
    private GameData data;

    private AsyncOperationHandle<SceneInstance> loadHandle;
    public event Action<double> OnNextPlanetPurchased;
    private int level;
    private Coroutine _moneyChecker;

    [Header("Money Checker")]
    [SerializeField] private GameObject _moneyDependentGO;      // объект, который нужно активировать/деактивировать
    [SerializeField] private double _moneyThreshold;           // минимальная сумма для активации
    [SerializeField] private float _checkInterval = 0.5f;

    //private void Start()
    //{
    //    for (int i = 0; i < _levelButtons.Count; i++)
    //    {
    //        _levelButtons[i].interactable = generalData.ActivePlanet > i;
    //    }
    //}

    private void Awake()
    {
        tipRectTransform = levelInfo.GetComponent<RectTransform>();
        tipCanvasGroup = levelInfo.GetComponent<CanvasGroup>();
        originalPosition = tipRectTransform.anchoredPosition;

    }

    private void Start()
    {
        level = SceneManager.GetActiveScene().buildIndex + 1;
        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! LevelController /// Start /// Scene Index: " + level);

        UpdateLevelButtons();
        UpdateBuyButtons();

        if (_moneyChecker == null)
            _moneyChecker = StartCoroutine(CheckMoneyRoutine());
    }

    private IEnumerator CheckMoneyRoutine()
    {
        // подождать, пока data окажется не null
        yield return new WaitUntil(() => data != null);

        while (true)
        {
            bool hasEnough = data.Money >= _moneyThreshold;
            _moneyDependentGO.SetActive(hasEnough);

            yield return new WaitForSeconds(_checkInterval);
        }
    }

    private void UpdateLevelButtons()
    {
        for (int i = 0; i < _levelButtons.Count; i++)
        {
            bool isActive = generalData.ActivePlanet > i;
            ColorBlock colors = _levelButtons[i].colors;
            colors.normalColor = isActive ? Color.white : disabledColor;
            colors.pressedColor = isActive ? enabledColor : disabledColor;
            colors.highlightedColor = isActive ? enabledColor : disabledColor;
            colors.selectedColor = isActive ? enabledColor : disabledColor;
            colors.disabledColor = disabledColor;
            _levelButtons[i].colors = colors;
        }
    }

    private void UpdateBuyButtons()
    {
        for (int i = 0; i < _buyButtons.Count; i++)
        {
            bool isTargetButton = i == level - 1;
            bool isActive = isTargetButton && data.Money > nextPlanetPrice && generalData.ActivePlanet == level;

            ColorBlock colors = _buyButtons[i].colors;
            colors.normalColor = isActive ? Color.white : disabledColor;
            colors.pressedColor = isActive ? enabledColor : disabledColor;
            colors.highlightedColor = isActive ? enabledColor : disabledColor;
            colors.selectedColor = isActive ? enabledColor : disabledColor;
            colors.disabledColor = disabledColor;

            _buyButtons[i].colors = colors;
        }

        for (int i = 0; i < _buyButtons.Count; i++)
        {
            if (generalData.ActivePlanet - 1 > i)
                _buyButtons[i].gameObject.SetActive(false);
        }
    }


    public void PurchaseNextPlanet()
    {
        if (data.Money > nextPlanetPrice && generalData.ActivePlanet == level)
        {
            OnNextPlanetPurchased.Invoke(nextPlanetPrice);
            _buyButtons[level - 1].gameObject.SetActive(false);
        }
        else
        {
            ShowTip();
        }
    }

    public void LoadSceene(int level)
    {
        if (generalData.ActivePlanet > level)
        {
            SceneManager.LoadScene(level);
            PlayerPrefs.SetInt("SavedScene", level);
            Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! UIManager /// LoadSceene /// Scene Index: " + level);
        }
        else
        {
            ShowTip();
        }
    }

    public void LoadRemoteSceene(string key)
    {
        loadHandle = Addressables.LoadSceneAsync(key, LoadSceneMode.Single);
    }

    public void PrepareGameData(GeneralGameData generalGameData, GameData gameData)
    {
        generalData = generalGameData;
        data = gameData;
        UpdateLevelButtons();
        UpdateBuyButtons();
    }

    public void ShowTip()
    {
        if (isHidingTip)
        {
            DOTween.Sequence()
                .AppendInterval(duration)
                .OnComplete(() => ShowTip());
            return;
        }

        levelInfo.SetActive(true);

        float startX = originalPosition.x + 100f;
        tipRectTransform.anchoredPosition = new Vector2(startX, originalPosition.y);
        tipCanvasGroup.alpha = 0f;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(tipRectTransform.DOAnchorPosX(originalPosition.x, duration));
        sequence.Join(tipCanvasGroup.DOFade(1f, duration));

        StartCoroutine(HideTip());
    }

    private IEnumerator HideTip()
    {       
        yield return new WaitForSeconds(2f);
        isHidingTip = true;
        Sequence sequence = DOTween.Sequence();
        sequence.Append(tipRectTransform.DOAnchorPosX(originalPosition.x + 100f, duration));
        sequence.Join(tipCanvasGroup.DOFade(0f, duration));
        sequence.OnComplete(() =>
        {
            levelInfo.SetActive(false);
            isHidingTip = false;
        });
    }

}
