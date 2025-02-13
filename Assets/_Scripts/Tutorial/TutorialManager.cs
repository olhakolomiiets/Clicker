using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;
using Lean.Localization;

public class TutorialManager : MonoBehaviour
{
    [SerializeField] private static TutorialManager Instance;
    [Header("Tutorial")]
    [SerializeField] private GameObject tutorialPointerPrefab;
    [SerializeField] private Transform[] tutorialSteps;
    [SerializeField] private GameObject tutorialInfo;
    [SerializeField] private TextMeshProUGUI tutorialStepTitle;
    [SerializeField] private TextMeshProUGUI tutorialStepText;

    [Header("Game Tips")]
    [SerializeField] private GameObject tipInfo;

    [Header("Other")]
    [SerializeField] private UIController uiController;

    private string TutorialStepKey = "TutorialStep";
    private string TutorialStepStateKey = "TutorialStepState";
    private string TutorialCompletedKey = "TutorialCompleted";
    private GameObject currentPointer;
    private int tutorialStep;
    private int stepState;
    private int activeIndex;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        LoadTutorialState();
        ShowNextTutorial();
    }

    private void LoadTutorialState()
    {
        tutorialStep = PlayerPrefs.GetInt(TutorialStepKey, 0);
        stepState = PlayerPrefs.GetInt(TutorialStepStateKey, 0);
    }

    private void SaveTutorialState()
    {
        PlayerPrefs.SetInt(TutorialStepKey, tutorialStep);
        PlayerPrefs.SetInt(TutorialStepStateKey, stepState);
        PlayerPrefs.Save();
    }

    public void ShowNextTutorial()
    {
        if (tutorialStep == 0)
            ShowTutorialInfo(tutorialStep);

        if (uiController.isDisplayed)
        {
            ResetPointer();

            if (HandleTutorialExitCondition())
                return;     
            
            if (tutorialStep < tutorialSteps.Length)
            {
                ShowPointer(tutorialSteps[tutorialStep]);
            }

            ShowTutorialInfo(tutorialStep);
            SaveTutorialState();
        }
        else ShowFirstStep();       
    }

    public void ShowNextTutorial(int step)
    {
        LoadTutorialState();

        tutorialStep = step;

        ResetPointer();

        if (HandleTutorialExitCondition())
            return;

        ShowPointer(tutorialSteps[tutorialStep]);
        ShowTutorialInfo(tutorialStep);
        SaveTutorialState();
    }

    public void ShowNextTutorial(int step, int state)
    {
        if (uiController.isDisplayed)
        {
            tutorialStep = step;
            stepState = state;

            ResetPointer();

            if (HandleTutorialExitCondition())
                return;

            ShowPointer(tutorialSteps[tutorialStep]);
            ShowTutorialInfo(tutorialStep);
            SaveTutorialState();
        }
        else ShowFirstStep();
    }

    public void ShowFirstStep()
    {
        ResetPointer();
        ShowPointer(tutorialSteps[0]);
    }

    private bool HandleTutorialExitCondition()
    {
        if ((tutorialStep == 5 && stepState == 0) || (tutorialStep == 6 && stepState == 0))
        {
            tutorialInfo.SetActive(false);
            return true;
        }
        return false;
    }

    private void ShowPointer(Transform target)
    {
        if (target == null)
            return;

        currentPointer = Instantiate(tutorialPointerPrefab, target.position, Quaternion.identity, target);
    }

    public void ResetPointer()
    {
        if (currentPointer != null)
            Destroy(currentPointer);
    }

    public void ShowGameTip(int i)
    {

        activeIndex = i;
        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! TutorialManager /// ShowGameTip /// Before ResetPointer /// Tutorial Step: " + tutorialStep);

        if ((tutorialStep == 5))
            return;

        ResetPointer();

        if (!uiController.isDisplayed)
            ShowPointer(tutorialSteps[0]);

        tipInfo.SetActive(true);

        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! TutorialManager /// ShowGameTip /// After ResetPointer /// Tutorial Step: " + tutorialStep);
    }

    public void HideGameTip()
    {
        if ((tutorialStep < 6))
            return;

        ResetPointer();

        tipInfo.SetActive(false);

        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! TutorialManager /// HideGameTip /// Tutorial Step: " + tutorialStep);
    }

    public void HideGameTip(int i)
    {
        if (activeIndex == i)
        {
            if ((tutorialStep < 6))
                return;

            ResetPointer();

            tipInfo.SetActive(false);
            Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! TutorialManager /// HideGameTip With Index /// Tutorial Step: " + tutorialStep);
        }       
    }

    public void AdvanceTutorial()
    {
        stepState = 0;
        tutorialStep++;
        SaveTutorialState();
        ShowNextTutorial();

        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! TutorialManager /// AdvanceTutorial /// Tutorial Step: " + tutorialStep);
    }

    public void ShowTutorialInfo(int step)
    {
        tutorialInfo.SetActive(true);
        tutorialStepTitle.text = $"{LeanLocalization.GetTranslationText("TitleTutorialStep" + step)}";
        tutorialStepText.text = $"{LeanLocalization.GetTranslationText("TextTutorialStep" + step)}";
    }
    public void OnTutorialCompleted()
    {
        PlayerPrefs.SetInt(TutorialCompletedKey, 1);
        tutorialInfo.SetActive(false);
        stepState = 0;
        tutorialStep++;
        SaveTutorialState();  
    }
}