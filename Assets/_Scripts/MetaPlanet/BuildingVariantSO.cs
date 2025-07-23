using UnityEngine;

[CreateAssetMenu(menuName = "PlanetBuilder/BuildingVariant")]
public class BuildingVariantSO : ScriptableObject
{
    public string variantName;
    public int price;
    public GameObject prefab;
    public Sprite icon; // для UI-кнопки
}
