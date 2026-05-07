using UnityEngine;

public class UpgradeClickHandler : MonoBehaviour
{
    public UpgradePanelUI panelUI;
    private MetaUpgradeItemController ctrl;

    void Awake() => ctrl = GetComponent<MetaUpgradeItemController>();

    void OnMouseDown()
    {
        panelUI.Open(ctrl);
    }
}
