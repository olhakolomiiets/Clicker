using UnityEngine;
using UnityEngine.UI;

public class TestMaterialButtonsUI : MonoBehaviour
{
    [SerializeField] private MetaPlanetMaterialController materialController;

    [Header("Test Buttons")]
    [SerializeField] private Button buyMaterial0Button;
    [SerializeField] private Button buyMaterial1Button;
    [SerializeField] private Button buyMaterial2Button;
    [SerializeField] private Button buyMaterial3Button;

    private void OnEnable()
    {
        buyMaterial0Button.onClick.AddListener(BuyMaterial0);
        buyMaterial1Button.onClick.AddListener(BuyMaterial1);
        buyMaterial2Button.onClick.AddListener(BuyMaterial2);
        buyMaterial3Button.onClick.AddListener(BuyMaterial3);
    }

    private void OnDisable()
    {
        buyMaterial0Button.onClick.RemoveListener(BuyMaterial0);
        buyMaterial1Button.onClick.RemoveListener(BuyMaterial1);
        buyMaterial2Button.onClick.RemoveListener(BuyMaterial2);
        buyMaterial3Button.onClick.RemoveListener(BuyMaterial3);
    }

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