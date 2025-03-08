using DG.Tweening;
using Lean.Localization;
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
    [SerializeField] private List<Button> _levelButtons;
    private GeneralGameData generalData;
    [SerializeField] private Color disabledColor = new Color(1, 1, 1, 0.5f);

    [SerializeField] private GameObject levelInfo;

    private CanvasGroup tipCanvasGroup;
    private RectTransform tipRectTransform;
    private Vector2 originalPosition;
    private float duration = 0.5f;
    private bool isHidingTip = false;

    private AsyncOperationHandle<SceneInstance> loadHandle;

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
        UpdateLevelButtons();
    }

    private void UpdateLevelButtons()
    {
        for (int i = 0; i < _levelButtons.Count; i++)
        {
            bool isActive = generalData.ActivePlanet > i;
            ColorBlock colors = _levelButtons[i].colors;
            colors.normalColor = isActive ? Color.white : disabledColor;
            _levelButtons[i].colors = colors;
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

        UpdateLevelButtons();

        //for (int i = 0; i < _levelButtons.Count; i++)
        //{
        //    _levelButtons[i].interactable = generalData.ActivePlanet > i;
        //}
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
