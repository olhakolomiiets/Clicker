using System;
using UnityEngine;

[CreateAssetMenu]

public class UpgradeItemData : ScriptableObject
{
    [field: SerializeField]
    public double ItemCost { get; private set; } = 3;
    [SerializeField] public int MaxCountIncrement;

    [field: SerializeField]
    public Sprite ItemImage { get; set; }

    [field: SerializeField]
    public string TranslationText { get; set; }
}
