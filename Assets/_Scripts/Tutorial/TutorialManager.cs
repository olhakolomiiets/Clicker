using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;
using Lean.Localization;
using DG.Tweening;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public class TutorialManager : MonoBehaviour
{
    [Header("Tutorial")]
    [SerializeField] private GameObject tutorialPointerPrefab;
    [SerializeField] private Transform[] tutorialSteps;
    [SerializeField] private GameObject tutorialInfo;
    [SerializeField] private GameObject infoWind;
    [SerializeField] private TextMeshProUGUI tutorialStepTitle;
    [SerializeField] private TextMeshProUGUI tutorialStepText;

    [SerializeField] private Button tutorialDismissButton; 
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
    private int tipIndex;

    private float duration = 0.5f;

    private bool isInfoActive;  
    private bool isHidingTip = false;
    private bool isHidingTutorial = false;

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

        if (SceneManager.GetActiveScene().buildIndex != 0)
            PlayerPrefs.SetInt(TutorialCompletedKey, 1);

        if (stepsCompleted <= 4)
            ShowNextTutorial();   
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

        PlayerPrefs.Save();
    }
    private void ShowPointer(Transform target)
    {
        if (target == null)
            return;

        ResetPointer();
        currentPointer = Instantiate(tutorialPointerPrefab, target.position, Quaternion.identity, target);
    }

    public void ResetPointer()
    {
        if (currentPointer != null)
        {          
            Destroy(currentPointer);
        }
    }

    #region TUTORIAL
    public void ShowNextTutorial()
    {
        if (tutorialStep == 0)
            ShowTutorialInfo(tutorialStep);

        if (tutorialStep == 5 && PlayerPrefs.GetInt("UpgradeTutorialShown") == 0 || tutorialStep == 6 && PlayerPrefs.GetInt("ShopTutorialShown") == 0)
        {
            if (uiController.isDisplayed)
            {
                ShowPointer(tutorialSteps[tutorialStep]);
            }
            else ShowFirstStep();

            return;
        }

        if (isHidingTutorial)
        {
            DOTween.Sequence()
                .AppendInterval(duration)
                .OnComplete(() => ShowNextTutorial());
            return;
        }

        if (uiController.isDisplayed)
        {
            if (HandleTutorialExitCondition())
                return;

            if (TutorialExitCondition())
                return;

            if (tutorialStep < tutorialSteps.Length)
                ShowPointer(tutorialSteps[tutorialStep]);
            
            ShowTutorialInfo(tutorialStep);

            stepsCompleted++;

            //Debug.Log($"TutorialManager /// ShowNextTutorial() /// tutorialStep: {tutorialStep} /// stepsCompleted: {stepsCompleted}");
            SaveTutorialState();
        }
        else ShowFirstStep();
    }

    public void ShowNextTutorial(int step)
    {
        if (isTipActive)
            HideTipInfo();

        if (step == 5 && PlayerPrefs.GetInt("UpgradeTutorialShown") == 0 || step == 6 && PlayerPrefs.GetInt("ShopTutorialShown") == 0)
        {
            if (uiController.isDisplayed)
            {
                ShowPointer(tutorialSteps[step]);
            }
            else ShowFirstStep();
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

            if (HandleTutorialExitCondition())
                return;

            if (TutorialExitCondition())
                return;

            ShowPointer(tutorialSteps[tutorialStep]);
            ShowTutorialInfo(tutorialStep);

            stepsCompleted++;

            //Debug.Log($"TutorialManager /// ShowNextTutorial(int step) /// tutorialStep: {tutorialStep} /// stepsCompleted: {stepsCompleted}");
            SaveTutorialState();
        }
        else ShowFirstStep();
    }

    public void ShowNextTutorial(int step, int state)
    {
        if (isTipActive)
            HideTipInfo();

        if (step == 5 && PlayerPrefs.GetInt("UpgradeTutorialShown") == 0 || step == 6 && PlayerPrefs.GetInt("ShopTutorialShown") == 0)
        {
            if (uiController.isDisplayed)
            {
                ShowPointer(tutorialSteps[step]);
            }
            else ShowFirstStep();
            return;
        }

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

            if (HandleTutorialExitCondition())
                return;

            if (TutorialExitCondition())
                return;

            ShowPointer(tutorialSteps[tutorialStep]);
            ShowTutorialInfo(tutorialStep);

            stepsCompleted++;

            //Debug.Log($"TutorialManager /// ShowNextTutorial(int step, int state) /// tutorialStep: {tutorialStep} /// stepsCompleted: {stepsCompleted}");
            SaveTutorialState();
        }
        else ShowFirstStep();
    }

    public void ShowTutorialStep(int step)
    {       
        if (isInfoActive)
        {
            StopCoroutine(HideTutorial());
            HideTutorialInfo();
        }           

        if (isTipActive)
            HideTipInfo();

        if (isHidingTutorial)
        {
            DOTween.Sequence()
                .AppendInterval(duration)
                .OnComplete(() => ShowTutorialStep(step));
            return;
        }

        ShowPointer(tutorialSteps[step]);
        ShowTutorialInfo(step);

        //Debug.Log($"TutorialManager /// ShowNextTutorial(int step) /// tutorialStep: {tutorialStep} /// stepsCompleted: {stepsCompleted}");
    }

    public void ShowShopTutorial(int step)
    {
        if (PlayerPrefs.GetInt(ShopTutorialKey) == 1)
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

        ResetPointer();
        ShowTutorialInfo(step);
        stepsCompleted++;

        PlayerPrefs.SetInt("ShopTutorialShown", 1); 
        SaveTutorialState();

        if (PlayerPrefs.GetInt("UpgradeTutorialShown") == 0)
            StartCoroutine(ShowTutorial(5));

        if (PlayerPrefs.GetInt("ShopTutorialShown") == 1 && PlayerPrefs.GetInt("UpgradeTutorialShown") == 1)
            StartCoroutine(HideTutorial());

        //Debug.Log($"TutorialManager /// ShowShopTutorial /// tutorialStep: {tutorialStep} /// stepsCompleted: {stepsCompleted}");
    }

    public void ShowUpgradeTutorial(int step)
    {
        if (PlayerPrefs.GetInt(UpgradeTutorialKey) == 1)
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

        ResetPointer();
        ShowTutorialInfo(step);
        stepsCompleted++;

        PlayerPrefs.SetInt("UpgradeTutorialShown", 1);
        SaveTutorialState();

        if (PlayerPrefs.GetInt("ShopTutorialShown") == 0)
            StartCoroutine(ShowTutorial(6));

        if (PlayerPrefs.GetInt("ShopTutorialShown") == 1 && PlayerPrefs.GetInt("UpgradeTutorialShown") == 1)
            StartCoroutine(HideTutorial());

        //Debug.Log($"TutorialManager /// ShowUpgradeTutorial /// tutorialStep: {tutorialStep} /// stepsCompleted: {stepsCompleted}");
    }

    public void ShowFirstStep()
    {        
        ShowPointer(tutorialSteps[0]);    
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
        if (tutorialStep == 5)
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

        //Debug.Log($"TutorialManager /// AdvanceTutorial /// tutorialStep: {tutorialStep}");

        SaveTutorialState();
        ShowNextTutorial();      
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

        if (stepsCompleted >= 6)
            PlayerPrefs.SetInt(TutorialCompletedKey, 1);
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
        }
    }

    private IEnumerator ShowTutorial(int step)
    {
        yield return new WaitForSeconds(2f);
        ShowPointer(tutorialSteps[step]);
    }

    private IEnumerator HideTutorial()
    {
        yield return new WaitForSeconds(4f);
        HideTutorialInfo();
    }

    #endregion

    #region GAME TIPS
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

            ShowTipInfo(tipIndex);
        }
    }

    public void HideGameTip(int i, int tip)
    {      
        if (activeIndex == i && tipIndex == tip)
        {
            isHidingTip = true;           
            HideTipInfo();
        }
    }

    public void ShowTipInfo(int index)
    {
        if (!isTipActive)
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
        }
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
    }

    #endregion
}