using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradePanelUI : MonoBehaviour
{
    public TextMeshProUGUI LevelText, BonusDescription, CurrentBonusText, NextBonusText, NextBonusDescription, CostText;
    public Image icon;
    public Button UpgradeButton;

    private MetaUpgradeItemController ctrl;

    private DragRotateGPT _planetRotator;

    void OnEnable()
    {
        _planetRotator = FindAnyObjectByType<DragRotateGPT>();
    }

    public void Open(MetaUpgradeItemController controller)
    {
        ctrl = controller;
        Refresh();
        gameObject.SetActive(true);
        _planetRotator.enabled = false;
        UpgradeButton.onClick.RemoveAllListeners();
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

        LevelText.text = $"Level: {lvl + 1}/{data.Count}";
        CurrentBonusText.text = $"Bonus: {data[lvl].BonusValue}";
        BonusDescription.text = data[lvl].BonusDescription;

        if (lvl < data.Count - 1)
        {
            var next = data[lvl + 1];
            NextBonusText.text = $"Next: {next.BonusValue}";
            NextBonusDescription.text = next.BonusDescription;
            CostText.text = next.UpgradeCost.ToString();
            UpgradeButton.interactable = ctrl.CanUpgrade();
        }
        else
        {
            NextBonusText.text = "Max";
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
        _planetRotator.enabled = true;
        gameObject.SetActive(false);
    }
}
