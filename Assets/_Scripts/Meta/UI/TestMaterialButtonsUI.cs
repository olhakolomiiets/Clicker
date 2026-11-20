using UnityEngine;
using UnityEngine.UI;

public class TestMaterialButtonsUI : MonoBehaviour
{
    [SerializeField] private MetaPlanetMaterialController materialController;

    private void BuyMaterial0()
    {
        materialController.BuyMaterial(0);
    }

    private void BuyMaterial1()
    {
        materialController.BuyMaterial(1);
    }

    private void BuyMaterial2()
    {
        materialController.BuyMaterial(2);
    }

    private void BuyMaterial3()
    {
        materialController.BuyMaterial(3);
    }
}