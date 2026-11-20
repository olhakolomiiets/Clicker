using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lean.Localization;

public class UpgradePanelUI : MonoBehaviour
{
    public TextMeshProUGUI LevelText, BonusDescription, CurrentBonusText, NextBonusText, NextBonusDescription, CostText;

    public Image icon;
    public Button UpgradeButton;

    private MetaUpgradeItemController ctrl;
    private bool _tutorialPreview;

    private DragRotateGPT _planetRotator;


    void OnEnable()
    {
        _planetRotator = FindAnyObjectByType<DragRotateGPT>();
        LeanLocalization.OnLocalizationChanged += OnLocalizationChanged;
    }

    void OnDisable()
    {
        LeanLocalization.OnLocalizationChanged -= OnLocalizationChanged;
    }

    void OnLocalizationChanged()
    {
        if (ctrl != null)
            Refresh();
    }


    public void Open(MetaUpgradeItemController controller) => Open(controller, tutorialPreview: false);

    internal void OpenForTutorial(MetaUpgradeItemController controller) => Open(controller, tutorialPreview: true);

    private void Open(MetaUpgradeItemController controller, bool tutorialPreview)
    {
        ctrl = controller;
        _tutorialPreview = tutorialPreview;
        Refresh();
        gameObject.SetActive(true);
        _planetRotator.enabled = false;
        UpgradeButton.onClick.RemoveAllListeners();

        if (!_tutorialPreview)
            UpgradeButton.onClick.AddListener(OnUpgradeClicked);
    }

    public void UpdateState(int index)
    {
        //bool bought = SaveSystem.IsVariantBought(controller.gameObject.name, index);
        //bool canAfford = CurrencyManager.Instance.Coins >= data.price;
        //GetComponent<Button>().interactable = bought || canAfford;
        // ����� ������ ����/������ � ����������� �� bought/canAfford
    }


    void Refresh()
    {
        var lvl = ctrl.currentLevel;
        var data = ctrl.data.Upgrades;

        if (icon != null && data != null && lvl >= 0 && lvl < data.Count)
        {
            int iconIndex = lvl < data.Count - 1
                ? lvl + 1
                : lvl;

            icon.sprite = data[iconIndex].ItemIcon;
        }

        LevelText.text = string.Format(
            LeanLocalization.GetTranslationText("upgrade_level"),
            lvl + 1,
            data.Count
        );

        CurrentBonusText.text = LeanLocalization.GetTranslationText("upgrade_current_bonus");

        BonusDescription.text = LeanLocalization.GetTranslationText(
            data[lvl].BonusDescription,
            data[lvl].BonusDescription
        );

        if (lvl < data.Count - 1)
        {
            var next = data[lvl + 1];

            NextBonusText.text = LeanLocalization.GetTranslationText("upgrade_next_bonus");

            NextBonusDescription.text = LeanLocalization.GetTranslationText(
                next.BonusDescription,
                next.BonusDescription
            );

            CostText.text = next.UpgradeCost.ToString();
            UpgradeButton.interactable = !_tutorialPreview && ctrl.CanUpgrade();
        }
        else
        {
            NextBonusText.text = LeanLocalization.GetTranslationText("upgrade_max");
            NextBonusDescription.text = "";
            CostText.text = "";
            UpgradeButton.interactable = false;
        }
    }

    void OnUpgradeClicked()
    {
        ctrl.Upgrade();
        Refresh();
    }

    public void Close()
    {
        if (_tutorialPreview)
            return;

        if (_planetRotator != null)
            _planetRotator.enabled = true;

        gameObject.SetActive(false);
    }

    internal void CloseTutorialPreview()
    {
        if (!_tutorialPreview)
            return;

        _tutorialPreview = false;
        Close();
    }
}
