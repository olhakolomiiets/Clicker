using System.Collections;
using UnityEngine;

public class EnableDirectChildren : MonoBehaviour
{
    [Tooltip("Список родительских объектов (будут активированы только их прямые дети)")]
    public Transform[] parents;

    [Tooltip("Задержка перед включением детей (в секундах)")]
    public float delay = 1f;

    void Start()
    {
        StartCoroutine(EnableChildrenAfterDelay(delay));
    }

    /// <summary>
    /// Включает только прямых детей каждого родителя через задержку.
    /// </summary>
    private IEnumerator EnableChildrenAfterDelay(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);

        foreach (Transform parent in parents)
        {
            if (parent == null) continue;

            foreach (Transform child in parent)
            {
                child.gameObject.SetActive(true); // Только прямой ребёнок
            }
        }
    }
}
