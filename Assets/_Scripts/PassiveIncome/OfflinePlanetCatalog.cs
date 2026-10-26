using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "OfflinePlanetCatalog",
    menuName = "Clicker/Passive Income/Planet Catalog")]
public class OfflinePlanetCatalog : ScriptableObject
{
    public List<OfflinePlanetEntry> Planets = new();

    public List<MetaUpgradeItemData> MetaUpgradeConfigs = new();
}

[Serializable]
public class OfflinePlanetEntry
{
    public string SaveKey;
    public int SceneBuildIndex;
    public List<ItemData> Items = new();
}