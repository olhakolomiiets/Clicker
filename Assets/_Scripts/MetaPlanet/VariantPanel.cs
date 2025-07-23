using UnityEngine;

public class VariantPanel : MonoBehaviour
{
    [Header("UI Prefab & Container")]
    [SerializeField] private VariantButtonUI buttonPrefab;
    [SerializeField] private RectTransform contentContainer;  // <-- сюда перетащите Content

    private BuildingController currentBuilding;

    public void OpenFor(BuildingController building)
    {
        currentBuilding = building;

        // Удаляем старые кнопки
        foreach (Transform t in contentContainer)
            Destroy(t.gameObject);

        // Создаём новые как дочерние Content
        for (int i = 0; i < building.variants.Count; i++)
        {
            var btn = Instantiate(buttonPrefab, contentContainer);
            btn.Init(building.variants[i], i, building);
        }
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
