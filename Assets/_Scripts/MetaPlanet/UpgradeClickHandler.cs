using UnityEngine;

public class UpgradeClickHandler : MonoBehaviour
{
    public UpgradePanelUI panelUI;
    private BuildingUpgradeController ctrl;

    void Awake() => ctrl = GetComponent<BuildingUpgradeController>();

    void OnMouseDown()
    {
        panelUI.Open(ctrl);
    }
}
