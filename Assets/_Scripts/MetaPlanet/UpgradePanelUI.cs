using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradePanelUI : MonoBehaviour
{
    public TextMeshProUGUI LevelText, BonusDescription, CurrentBonusText, NextBonusText, NextBonusDescription, CostText;
    public Button UpgradeButton;

    private MetaUpgradeItemController ctrl;

    public void Open(MetaUpgradeItemController controller)
    {
        ctrl = controller;
        Refresh();
        gameObject.SetActive(true);
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

        LevelText.text = $"Level: {lvl}/{data.Count}";
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
        gameObject.SetActive(false);
    }
}
