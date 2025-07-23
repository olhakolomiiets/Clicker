using UnityEngine;

public class BuildingClickHandler : MonoBehaviour
{
    public VariantPanel panel;
    private BuildingController ctrl;

    void Awake() => ctrl = GetComponent<BuildingController>();
    void OnMouseDown() => panel.OpenFor(ctrl);
}
