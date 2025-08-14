using UnityEngine;

public class VariantPanel : MonoBehaviour
{
    [Header("UI Prefab & Container")]
    [SerializeField] private VariantButtonUI buttonPrefab;
    [SerializeField] private RectTransform contentContainer;
    private MetaVariantItemController itemController;

    public void OpenFor(MetaVariantItemController item)
    {
        itemController = item;

        foreach (Transform t in contentContainer)
            Destroy(t.gameObject);

        for (int i = 0; i < item.Variants.Count; i++)
        {
            var btn = Instantiate(buttonPrefab, contentContainer);
            btn.Init(item.Variants[i], i, item);
        }
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
