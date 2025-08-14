using UnityEngine;
using System.Collections.Generic;

public class MetaUpgradeItemController : MonoBehaviour
{
    public MetaUpgradeItemData data;
    [SerializeField] private List<GameObject> activatesPerLevel;
    [SerializeField] private List<GameObject> deactivatesPerLevel;

    [SerializeField] private UpgradePanelUI panelUI;
    public UpgradePanelUI PanelUI => panelUI;

    [HideInInspector] public int currentLevel;

    public void PrepareItemData(int level)
    {
        currentLevel = level;
    }

    void Start()
    {
        ApplyLevel(currentLevel);
    }

    public bool CanUpgrade()
    {
        return currentLevel < data.Upgrades.Count - 1;
    }

    public void Upgrade()
    {
        if (!CanUpgrade()) return;
        var next = currentLevel + 1;
        var lvlData = data.Upgrades[next];

        currentLevel = next;
        ApplyLevel(currentLevel);
    }

    void ApplyLevel(int lvl)
    {
        Debug.Log($"[Upgrade] {gameObject.name}  level {lvl}, bonus = {data.Upgrades[lvl].BonusValue}");

        for (int i = 0; i < activatesPerLevel.Count; i++)
            activatesPerLevel[i].SetActive(i <= lvl);
        for (int i = 0; i < deactivatesPerLevel.Count; i++)
            deactivatesPerLevel[i].SetActive(i > lvl);
    }

    void OnMouseDown()
    {
        panelUI.Open(this);
    }
}

