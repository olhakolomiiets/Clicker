using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;
using Lean.Localization;
using DG.Tweening;
using System.Collections;

public class TutorialManager : MonoBehaviour
{
    //[SerializeField] private static TutorialManager Instance;
    [Header("Tutorial")]
    [SerializeField] private GameObject tutorialPointerPrefab;
    [SerializeField] private Transform[] tutorialSteps;
    [SerializeField] private GameObject tutorialInfo;
    [SerializeField] private GameObject infoWind;
    [SerializeField] private TextMeshProUGUI tutorialStepTitle;
    [SerializeField] private TextMeshProUGUI tutorialStepText;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector3 originalScale;

    [Header("Game Tips")]
    [SerializeField] private GameObject tipInfo;
    [SerializeField] private TextMeshProUGUI gameTipText;
    private CanvasGroup tipCanvasGroup;
    private RectTransform tipRectTransform;
    private Vector2 originalPosition;
    private bool isTipActive;

    [Header("Other")]
    [SerializeField] private UIController uiController;

    private string TutorialStepKey = "TutorialStep";
    private string TutorialStepStateKey = "TutorialStepState";
    private string TutorialStepsCompletedKey = "TutorialStepsCompleted";
    private string TutorialCompletedKey = "TutorialCompleted";
    private string ShopTutorialKey = "ShopTutorialShown";
    private string UpgradeTutorialKey = "UpgradeTutorialShown";

    private GameObject currentPointer;
    private int tutorialStep;
    private int stepsCompleted;
    private int stepState;
    private int activeIndex;
    private float duration = 0.5f;
    private bool isInfoActive;
    private int tipIndex;
    private bool isHidingTip = false;
    private bool isHidingTutorial = false;
    private bool isSecondVisit = false;

    private void Awake()
    {
        tipRectTransform = tipInfo.GetComponent<RectTransform>();
        tipCanvasGroup = tipInfo.GetComponent<CanvasGroup>();
        originalPosition = tipRectTransform.anchoredPosition;

        rectTransform = infoWind.GetComponent<RectTransform>();
        canvasGroup = infoWind.GetComponent<CanvasGroup>();
        originalScale = rectTransform.localScale;
    }

    private void Start()
    {
        LoadTutorialState();

        if (tutorialStep >= 7)
            PlayerPrefs.SetInt(TutorialCompletedKey, 1);

        if (stepsCompleted <= 4)
            ShowNextTutorial();
        else
        {
            isSecondVisit = true;
            ShowSecondVisitUpgradeTutorial();
        }      
    }

    private void LoadTutorialState()
    {
        tutorialStep = PlayerPrefs.GetInt(TutorialStepKey, 0);
        stepState = PlayerPrefs.GetInt(TutorialStepStateKey, 0);
        stepsCompleted = PlayerPrefs.GetInt(TutorialStepsCompletedKey, 0);
    }

    private void SaveTutorialState()
    {
        PlayerPrefs.SetInt(TutorialStepKey, tutorialStep);
        PlayerPrefs.SetInt(TutorialStepStateKey, stepState);
        PlayerPrefs.SetInt(TutorialStepsCompletedKey, stepsCompleted);

        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! TutorialManager /// SaveTutorialState /// Tutorial Step: " + tutorialStep + " Tutorial Steps Completed: " + stepsCompleted);
        PlayerPrefs.Save();
    }
    private void ShowPointer(Transform target)
    {
        if (target == null)
            return;
    
        currentPointer = Instantiate(tutorialPointerPrefab, target.position, Quaternion.identity, target);
        Debug.Log("TutorialManager /// ShowPointer /// Instantiating Pointer at " + target.position);
    }

    public void ResetPointer()
    {
        if (currentPointer != null)
        {          
            Destroy(currentPointer);
            Debug.Log("TutorialManager /// ResetPointer /// Destroying current pointer");
        }
    }

    #region TUTORIAL
    public void ShowNextTutorial()
    {
        if (tutorialStep == 0)
            ShowTutorialInfo(tutorialStep);

        if (isHidingTutorial)
        {
            DOTween.Sequence()
                .AppendInterval(duration)
                .OnComplete(() => ShowNextTutorial());
            return;
        }

        if (uiController.isDisplayed)
        {
            ResetPointer();

            if (HandleTutorialExitCondition())
                return;

            if (TutorialExitCondition())
                return;

            if (tutorialStep < tutorialSteps.Length)
                ShowPointer(tutorialSteps[tutorialStep]);
            
            ShowTutorialInfo(tutorialStep);

            stepsCompleted++;
            SaveTutorialState();
        }
        else ShowFirstStep();

        Debug.Log($"TutorialManager /// ShowNextTutorial /// Current Step: {tutorialStep}");
    }

    public void ShowNextTutorial(int step)
    {
        if (isTipActive)
            HideTipInfo();

        if (step == 5)
        {
            ResetPointer();
            tutorialStep = step;
            ShowPointer(tutorialSteps[tutorialStep]);
            return;
        }

        if (isHidingTutorial)
        {
            DOTween.Sequence()
                .AppendInterval(duration)
                .OnComplete(() => ShowNextTutorial(step));
            return;
        }

        if (uiController.isDisplayed)
        {
            tutorialStep = step;
            ResetPointer();

            if (HandleTutorialExitCondition())
                return;

            if (TutorialExitCondition())
                return;

            ShowPointer(tutorialSteps[tutorialStep]);
            ShowTutorialInfo(tutorialStep);

            stepsCompleted++;
            SaveTutorialState();
        }
        else ShowFirstStep();

        Debug.Log($"TutorialManager /// ShowNextTutorial /// Step: {step}");
    }

    public void ShowNextTutorial(int step, int state)
    {
        if (isTipActive)
            HideTipInfo();

        if (isHidingTutorial)
        {
            DOTween.Sequence()
                .AppendInterval(duration)
                .OnComplete(() => ShowNextTutorial(step, state));
            return;
        }

        if (uiController.isDisplayed)
        {
            tutorialStep = step;
            stepState = state;
            ResetPointer();

            if (HandleTutorialExitCondition())
                return;

            if (TutorialExitCondition())
                return;

            ShowPointer(tutorialSteps[tutorialStep]);
            ShowTutorialInfo(tutorialStep);

            stepsCompleted++;
            SaveTutorialState();
        }
        else ShowFirstStep();

        Debug.Log($"TutorialManager /// ShowNextTutorial /// Step: {step}, State: {state}");
    }

    public void ShowShopTutorial(int step)
    {
        if (PlayerPrefs.GetInt(ShopTutorialKey, 0) == 1)
            return;

        if (isTipActive)
            HideTipInfo();

        if (isInfoActive)
            HideTutorialInfo();

        if (isHidingTutorial)
        {
            DOTween.Sequence()
                .AppendInterval(duration)
                .OnComplete(() => ShowShopTutorial(step));
            return;
        }
        ShowTutorialInfo(step);
        stepsCompleted++;

        if (stepsCompleted >= 5)
            tutorialStep++;

        PlayerPrefs.SetInt("ShopTutorialShown", 1); 
        SaveTutorialState();

        Debug.Log($"TutorialManager /// ShowShopTutorial /// Step: {step} /// if (isShopTutorialActive == false)");
    }

    public void ShowUpgradeTutorial(int step)
    {
        if (PlayerPrefs.GetInt(UpgradeTutorialKey, 0) == 1)
            return;

        if (isTipActive)
            HideTipInfo();

        if (isInfoActive)
            HideTutorialInfo();

        if (isHidingTutorial)
        {
            DOTween.Sequence()
                .AppendInterval(duration)
                .OnComplete(() => ShowUpgradeTutorial(step));
            return;
        }

        ShowTutorialInfo(step);
        stepsCompleted++;

        if (stepsCompleted >= 5)
            tutorialStep++;

        PlayerPrefs.SetInt("UpgradeTutorialShown", 1);
        SaveTutorialState();

        StartCoroutine(ShowSecondVisitShopTutorial());

        Debug.Log($"TutorialManager /// ShowShopTutorial /// Step: {step} /// if (isShopTutorialActive == false)");
    }

    public void ShowFirstStep()
    {        
        ResetPointer();
        ShowPointer(tutorialSteps[0]);
        Debug.Log("TutorialManager /// ShowFirstStep");
    }

    private bool HandleTutorialExitCondition()
    {
        if (tutorialStep == 3 && stepState == 0 || tutorialStep == 4 && stepState == 0)
        {
            HideTutorialInfo();
            return true;
        }
        return false;
    }

    private bool TutorialExitCondition()
    {
        if (tutorialStep == 5 && !isSecondVisit)
        {
            HideTutorialInfo();
            return true;
        }
        return false;
    }

    public void AdvanceTutorial()
    {     
        isInfoActive = false;
        stepState = 0;
        tutorialStep++;

        SaveTutorialState();
        ShowNextTutorial();

        Debug.Log($"TutorialManager /// AdvanceTutorial /// Current Step: {tutorialStep}");
    }

    public void HideTutorialInfo()
    {
        isInfoActive = false;
        isHidingTutorial = true;
        Sequence sequence = DOTween.Sequence();
        sequence.Append(rectTransform.DOScale(Vector3.zero, duration).SetEase(Ease.InBack));
        sequence.Join(canvasGroup.DOFade(0f, duration));
        sequence.OnComplete(() =>
        {
            tutorialInfo.SetActive(false);
            isHidingTutorial = false;
        });
        ResetPointer();

        if (tutorialStep >= 7)
            PlayerPrefs.SetInt(TutorialCompletedKey, 1);

        Debug.Log("TutorialManager /// HideTutorialInfo");
    }

    public void ShowTutorialInfo(int step)
    {
        if (!isInfoActive)
        {
            isInfoActive = true;
            tutorialInfo.SetActive(true);
            tutorialStepTitle.text = $"{LeanLocalization.GetTranslationText("TitleTutorialStep" + step)}";
            tutorialStepText.text = $"{LeanLocalization.GetTranslationText("TextTutorialStep" + step)}";

            rectTransform.localScale = Vector3.zero;
            canvasGroup.alpha = 0f;

            Sequence sequence = DOTween.Sequence();
            sequence.Append(rectTransform.DOScale(originalScale, duration).SetEase(Ease.OutBack));
            sequence.Join(canvasGroup.DOFade(1f, duration));

            Debug.Log($"TutorialManager /// ShowTutorialInfo /// Step: {step}");
        }
    }

    public void ShowSecondVisitUpgradeTutorial()
    {          
        if (PlayerPrefs.GetInt(UpgradeTutorialKey, 0) == 0)
        {
            if (uiController.isDisplayed)
            {
                tutorialStep = 5;
                ResetPointer();

                ShowPointer(tutorialSteps[tutorialStep]);
                ShowTutorialInfo(tutorialStep);

                stepsCompleted++;
                SaveTutorialState();
            }
            else ShowFirstStep();           
        }      
    }

    private IEnumerator ShowSecondVisitShopTutorial()
    {
        yield return new WaitForSeconds(2f);
        ResetPointer();
        ShowPointer(tutorialSteps[6]);
    }

    #endregion

    #region GAME TIPS

    public void ShowFirstPointer()
    {      
        if (isTipActive && stepsCompleted > 6)
        {
            ResetPointer();
            ShowPointer(tutorialSteps[0]);
            Debug.Log("TutorialManager /// ShowFirstPointer /// isTipActive: " + isTipActive + ", stepsCompleted: " + stepsCompleted);
        }
    }

    public void ShowGameTip(int i, int tip)
    {       
        if (tutorialStep == 3)
            return;

        if (isHidingTip)
        {
            DOTween.Sequence()
                .AppendInterval(duration)
                .OnComplete(() => ShowGameTip(i, tip));
            return;
        }
        
        if (!isTipActive && !isInfoActive)
        {       
            activeIndex = i;
            tipIndex = tip;

            if (!uiController.isDisplayed)
                ShowPointer(tutorialSteps[0]);

            ShowTipInfo(tipIndex);
            Debug.Log($"TutorialManager /// ShowGameTip /// i: {i}, tip: {tip}, tutorialStep: {tutorialStep}");
        }
    }

    public void HideGameTip(int i, int tip)
    {      
        if (activeIndex == i && tipIndex == tip)
        {
            isHidingTip = true;           
            HideTipInfo();
            ResetPointer();
            Debug.Log($"TutorialManager /// HideGameTip (index, tip) /// i: {i}, tip: {tip}, activeIndex: {activeIndex}, tipIndex: {tipIndex}");
        }
    }

    public void ShowTipInfo(int index)
    {
        isTipActive = true;
        tipInfo.SetActive(true);
        
        if (index == 0)
            gameTipText.text = $"{LeanLocalization.GetTranslationText("GameTip")}";
        else
            gameTipText.text = $"{LeanLocalization.GetTranslationText("ManagerTip")}";

        float startX = originalPosition.x + 100f;
        tipRectTransform.anchoredPosition = new Vector2(startX, originalPosition.y);
        tipCanvasGroup.alpha = 0f;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(tipRectTransform.DOAnchorPosX(originalPosition.x, duration));
        sequence.Join(tipCanvasGroup.DOFade(1f, duration));
        Debug.Log($"TutorialManager /// ShowTipInfo /// index: {index}");
    }

    public void HideTipInfo()
    {
        isTipActive = false;
        Sequence sequence = DOTween.Sequence();
        sequence.Append(tipRectTransform.DOAnchorPosX(originalPosition.x + 100f, duration));
        sequence.Join(tipCanvasGroup.DOFade(0f, duration));
        sequence.OnComplete(() =>
        {
            tipInfo.SetActive(false);
            isHidingTip = false;
        });

        Debug.Log("TutorialManager /// HideTipInfo");
    }

    #endregion
}