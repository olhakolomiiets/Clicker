using UnityEngine;
using System.Collections.Generic;

public class BuildingUpgradeController : MonoBehaviour
{
    [Header("Data & References")]
    public UpgradableBuildingSO data;
    public List<GameObject> activatesPerLevel;   // список GO, которые активировать при каждом уровне
    public List<GameObject> deactivatesPerLevel; // GO, которые деактивировать

    [HideInInspector] public int currentLevel;

    void Start()
    {
        // загрузить уровень из сохранения и применить
        //currentLevel = SaveSystem.GetUpgradeLevel(gameObject.name);
        ApplyLevel(currentLevel);
    }

    public bool CanUpgrade()
    {
        return currentLevel < data.levels.Count - 1;
            //&& CurrencyManager.Instance.Coins >= data.levels[currentLevel + 1].cost;
    }

    public void Upgrade()
    {
        if (!CanUpgrade()) return;
        var next = currentLevel + 1;
        var lvlData = data.levels[next];

        //if (!CurrencyManager.Instance.Spend(lvlData.cost)) return;
        currentLevel = next;
        //SaveSystem.MarkUpgradeLevel(gameObject.name, currentLevel);
        ApplyLevel(currentLevel);
    }

    void ApplyLevel(int lvl)
    {
        // Debug-место для применения бонуса
        Debug.Log($"[Upgrade] {gameObject.name}  level {lvl}, bonus = {data.levels[lvl].bonusValue}");

        // переключить GO
        for (int i = 0; i < activatesPerLevel.Count; i++)
            activatesPerLevel[i].SetActive(i <= lvl);
        for (int i = 0; i < deactivatesPerLevel.Count; i++)
            deactivatesPerLevel[i].SetActive(i > lvl);
    }
}
