using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManagerController : MonoBehaviour
{
    [SerializeField] private GameObject _managerButton;       
    [SerializeField] private GameObject _buyManagersPanel;
    [SerializeField] private GameObject _buyManagerButtonPrefab;
    [SerializeField] private UISquadLeadersButton _buyButton;
    [SerializeField] private RectTransform _panelTransform;

    public event Action<int> OnManagerPurchased;

    private TutorialManager tutorialManager;

    private void Awake()
    {
        _buyManagersPanel = GameObject.FindGameObjectWithTag("ManagersPanel");
    }

    private void Start()
    {
        if (PlayerPrefs.GetInt("TutorialStepsCompleted") < 7)
            tutorialManager = FindAnyObjectByType<TutorialManager>();
    }

    public void AddButton(int index, float price, Sprite currency)
    {
        GameObject buttonObject = Instantiate(_buyManagerButtonPrefab, _buyManagersPanel.transform);
        _buyButton = buttonObject.GetComponent<UISquadLeadersButton>();

        _buyButton.SetValue(price, currency);
        int i = index;
        _buyButton.OnClicked += () => OnManagerPurchased?.Invoke(i);
        _buyButton.ToggleActive(false);

        _buyButton.gameObject.SetActive(false);
    }
    public void ToggleManagerPanel()
    { 
        _buyButton.gameObject.SetActive(!_buyButton.gameObject.activeSelf);

        if (PlayerPrefs.GetInt("TutorialStep") == 4 && tutorialManager != null)
            tutorialManager.AdvanceTutorial();

        if (_buyButton.gameObject.activeSelf == false)
            return;
    }

    public void ToggleButton(int index, bool val)
        => _buyButton.ToggleActive(val);

    internal void SetButtonPurchased(int index, bool val)
    {
        if(val)
        {
            _buyButton.SetPurchasedImage();
            _managerButton.SetActive(false);
            //_panelTransform.sizeDelta = new Vector2(980, 185);
            _buyButton.gameObject.SetActive(false);

        }

    }
}
