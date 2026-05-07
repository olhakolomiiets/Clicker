using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PlanetBuilder/PlanetMaterialData")]
public class MetaPlanetMaterialData : ScriptableObject
{
    [SerializeField] public List<PlanetMaterialVariant> Materials;

    public double MaterialPrice(int index) => Materials[index].Price;
}

[Serializable]
public class PlanetMaterialVariant
{
    public string Name;

    [Header("Materials")]
    public Material LargeObjectsMaterial;
    public Material SmallObjectsMaterial;

    [Header("UI")]
    public double Price;
    public Sprite Icon;
}