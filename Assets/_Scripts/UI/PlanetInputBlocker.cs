using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlanetInputBlocker : MonoBehaviour
{
    private static readonly HashSet<PlanetInputBlocker> activeBlockers = new();

    private void OnEnable()
    {
        activeBlockers.Add(this);
    }

    private void OnDisable()
    {
        activeBlockers.Remove(this);
    }

    public static bool IsBlocked()
    {
        if (activeBlockers.Count > 0)
            return true;

        if (EventSystem.current == null)
            return false;

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            if (result.module is GraphicRaycaster)
                return true;
        }

        return false;
    }
}