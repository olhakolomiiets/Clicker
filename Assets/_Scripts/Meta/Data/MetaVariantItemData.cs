using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PlanetBuilder/VariantItemData")]
public class MetaVariantItemData : ScriptableObject
{
    [Header("Base Settings")]
    [SerializeField] public string ItemName;
    [SerializeField] public int Quantity;
    [SerializeField] public GameObject BoxPrefab;
    [SerializeField] public Sprite Icon;

    [Header("Variants")]
    [SerializeField] public List<ItemVariant> Variants;

    public double VariantItemPrice(int index) => Variants[index].Price;

}

[Serializable]
public class ItemVariant
{
    public string VariantName;
    public GameObject Prefab;
    public double Price;
    public Sprite ItemIcon;
}

