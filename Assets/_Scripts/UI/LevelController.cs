using DG.Tweening;
using Firebase.Analytics;
using Lean.Localization;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using UnityEngine.AddressableAssets;
//using UnityEngine.ResourceManagement.AsyncOperations;
//using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelController : MonoBehaviour
{
    [SerializeField] private List<Button> _buyButtons;
    [SerializeField] private List<Button> _levelButtons;
    [SerializeField] private Color disabledColor = new Color(1, 1, 1, 0.5f);
    [SerializeField] private Color enabledColor = Color.white;

    [SerializeField] private GameObject levelInfo;
    [SerializeField] private bool isMetaPlanet = false;

    private CanvasGroup tipCanvasGroup;
    private RectTransform tipRectTransform;
    private Vector2 originalPosition;
    private float duration = 0.5f;
    private bool isHidingTip = false;

    [SerializeField] private double nextPlanetPrice;

    private GeneralGameData generalData;
    private GameData data;

    //private AsyncOperationHandle<SceneInstance> loadHandle;
    public event Action<double> OnNextPlanetPurchased;
    private int level;
    private Coroutine _moneyChecker;

    [Header("Money Checker")]
    [SerializeField] private GameObject _moneyDependentGO;
    [SerializeField] private float _checkInterval = 0.5f;

    private const int FreeScenesCount = 2;
    private const int FirstPaidSceneIndex = 2;

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
        yield return new WaitUntil(() => data != null && generalData != null);

        while (true)
        {
            bool isPaidScene = level >= FirstPaidSceneIndex;

            bool hasEnough =
                isPaidScene &&
                data.Money >= nextPlanetPrice &&
                generalData.ActivePlanet == level;

            if (_moneyDependentGO != null)
                _moneyDependentGO.SetActive(hasEnough);

            yield return new WaitForSeconds(_checkInterval);
        }
    }

    private void UpdateLevelButtons()
    {
        if (generalData == null)
            return;

        for (int i = 0; i < _levelButtons.Count; i++)
        {
            // В списке нет кнопки сцены 0:
            // i = 0 → сцена 1
            // i = 1 → сцена 2
            // i = 2 → сцена 3
            int sceneIndex = i + 1;

            bool isFree = sceneIndex < FreeScenesCount;
            bool isUnlocked = isFree || generalData.ActivePlanet > sceneIndex;

            _levelButtons[i].interactable = isUnlocked;

            ColorBlock colors = _levelButtons[i].colors;
            colors.normalColor = isUnlocked ? enabledColor : disabledColor;
            colors.pressedColor = isUnlocked ? enabledColor : disabledColor;
            colors.highlightedColor = isUnlocked ? enabledColor : disabledColor;
            colors.selectedColor = isUnlocked ? enabledColor : disabledColor;
            colors.disabledColor = disabledColor;

            _levelButtons[i].colors = colors;
        }
    }

    private void UpdateBuyButtons()
    {
        if (generalData == null || data == null)
            return;

        for (int i = 0; i < _buyButtons.Count; i++)
        {
            // i = 0 → покупка сцены 2
            // i = 1 → покупка сцены 3
            int paidSceneIndex = FirstPaidSceneIndex + i;

            bool alreadyPurchased = generalData.ActivePlanet > paidSceneIndex;
            bool isCurrentPurchase =
                level == paidSceneIndex &&
                generalData.ActivePlanet == paidSceneIndex;

            bool hasEnoughMoney = data.Money >= nextPlanetPrice;
            bool isActive = isCurrentPurchase && hasEnoughMoney;

            _buyButtons[i].gameObject.SetActive(!alreadyPurchased);

            ColorBlock colors = _buyButtons[i].colors;
            colors.normalColor = isActive ? enabledColor : disabledColor;
            colors.pressedColor = isActive ? enabledColor : disabledColor;
            colors.highlightedColor = isActive ? enabledColor : disabledColor;
            colors.selectedColor = isActive ? enabledColor : disabledColor;
            colors.disabledColor = disabledColor;

            _buyButtons[i].colors = colors;
        }
    }

    public void PurchaseNextPlanet()
    {
        if (data == null || generalData == null)
            return;

        int buyButtonIndex = level - FirstPaidSceneIndex;

        bool validButtonIndex =
            buyButtonIndex >= 0 &&
            buyButtonIndex < _buyButtons.Count;

        bool canPurchase =
            validButtonIndex &&
            data.Money >= nextPlanetPrice &&
            generalData.ActivePlanet == level;

        if (!canPurchase)
        {
            ShowTip();
            return;
        }

        OnNextPlanetPurchased?.Invoke(nextPlanetPrice);
        _buyButtons[buyButtonIndex].gameObject.SetActive(false);

        int planetNumber = level + 1;
        FirebaseAnalytics.LogEvent("bought_planet_" + planetNumber);

        UpdateLevelButtons();
        UpdateBuyButtons();
    }

    public void LoadSceene(int sceneIndex)
    {
        if (generalData == null)
            return;

        bool isFree = sceneIndex < FreeScenesCount;
        bool isUnlocked = isFree || generalData.ActivePlanet > sceneIndex;

        if (isUnlocked)
        {
            SceneManager.LoadScene(sceneIndex);
            PlayerPrefs.SetInt("SavedScene", sceneIndex);

            Debug.Log(
                "LevelController /// LoadSceene /// Scene Index: " +
                sceneIndex
            );
        }
        else
        {
            ShowTip();
        }
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
