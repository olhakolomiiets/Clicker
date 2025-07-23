using System.Collections.Generic;
using UnityEngine;

public class BuildingController : MonoBehaviour
{
    public List<BuildingVariantSO> variants;
    public int currentIndex = 0;
    private List<GameObject> instances = new List<GameObject>();

    void Start()
    {
        // —оздаем все префаб-экземпл€ры, но деактивируем
        foreach (var v in variants)
        {
            var go = Instantiate(v.prefab, transform);
            go.SetActive(false);
            instances.Add(go);
        }
        // јктивируем выбранный по умолчанию
        instances[currentIndex].SetActive(true);
    }

    public void SelectVariant(int index)
    {
        instances[currentIndex].SetActive(false);
        currentIndex = index;
        instances[currentIndex].SetActive(true);
    }
}
