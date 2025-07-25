using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradePanelUI : MonoBehaviour
{
    public TextMeshProUGUI LevelText, CurrentBonusText, NextBonusText, CostText;
    public Button UpgradeButton;

    private BuildingUpgradeController ctrl;

    public void Open(BuildingUpgradeController controller)
    {
        ctrl = controller;
        Refresh();
        gameObject.SetActive(true);
        UpgradeButton.onClick.RemoveAllListeners();
        UpgradeButton.onClick.AddListener(OnUpgradeClicked);
    }

    void Refresh()
    {
        var lvl = ctrl.currentLevel;
        var data = ctrl.data.levels;

        LevelText.text = $"Level: {lvl + 1}/{data.Count}";
        CurrentBonusText.text = $"Bonus: {data[lvl].bonusValue}";

        if (lvl < data.Count - 1)
        {
            var next = data[lvl + 1];
            NextBonusText.text = $"Next: {next.bonusValue}";
            CostText.text = $"Cost: {next.cost}";
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
