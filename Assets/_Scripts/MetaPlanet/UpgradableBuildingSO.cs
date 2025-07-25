using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "PlanetBuilder/UpgradableBuilding")]
public class UpgradableBuildingSO : ScriptableObject
{
    public List<LevelData> levels;
}

[System.Serializable]
public struct LevelData
{
    public int cost;           // цена апгрейда до этого уровня
    public float bonusValue;   // условный бонус (его применяем через Debug или в другом скрипте)
}
