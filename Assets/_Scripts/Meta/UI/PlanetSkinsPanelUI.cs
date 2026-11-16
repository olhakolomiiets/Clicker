using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlanetSkinsPanelUI : MonoBehaviour
{
    [SerializeField] private MetaPlanetMaterialController materialController;
    [SerializeField] private PlanetSkinCardUI cardPrefab;
    [SerializeField] private Transform content;

    private readonly List<PlanetSkinCardUI> cards = new();

    private void OnEnable()
    {
        if (materialController == null || cardPrefab == null || content == null)
        {
            Debug.LogError("PlanetSkinsPanelUI requires a material controller, card prefab and grid content.", this);
            return;
        }

        materialController.OnMaterialSelected += RefreshCards;

        if (cards.Count == 0)
            StartCoroutine(BuildCards());
        else
            RefreshCards();
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        if (materialController != null)
            materialController.OnMaterialSelected -= RefreshCards;
    }

    private IEnumerator BuildCards()
    {
        // The panel can start enabled before the material controller loads its save in Start.
        yield return null;

        var materials = materialController.Materials;
        for (int index = 0; index < materials.Count; index++)
        {
            var card = Instantiate(cardPrefab, content);
            card.Bind(materialController, index);
            cards.Add(card);
        }
    }

    private void RefreshCards()
    {
        foreach (var card in cards)
            card.Refresh();
    }
}
