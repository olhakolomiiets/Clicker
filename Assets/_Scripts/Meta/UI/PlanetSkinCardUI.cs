using Lean.Localization;
using PlanetBuilder.Messages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlanetSkinCardUI : MonoBehaviour
{
    [SerializeField] private Image skinIcon;
    [SerializeField] private TMP_Text skinName;
    [SerializeField] private Button actionButton;
    [SerializeField] private TMP_Text actionText;
    [SerializeField] private GameObject priceIcon;

    private MetaPlanetMaterialController materialController;
    private int materialIndex;

    private void OnEnable()
    {
        actionButton.onClick.AddListener(OnActionClicked);
        LeanLocalization.OnLocalizationChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        actionButton.onClick.RemoveListener(OnActionClicked);
        LeanLocalization.OnLocalizationChanged -= Refresh;
        MessageManager.Instance?.DiscardMessagesOwnedBy(MessageChannel.Toast, this);
    }

    public void Bind(MetaPlanetMaterialController controller, int index)
    {
        materialController = controller;
        materialIndex = index;

        PlanetMaterialVariant material = controller.Materials[index];
        skinIcon.sprite = material.Icon;
        Refresh();
    }

    public void Refresh()
    {
        if (materialController == null)
            return;

        PlanetMaterialVariant material = materialController.Materials[materialIndex];
        skinName.text = LeanLocalization.GetTranslationText(material.Name, material.Name);

        bool isActive = materialController.IsMaterialActive(materialIndex);
        bool isBought = materialController.IsMaterialBought(materialIndex);

        actionButton.interactable = !isActive;
        priceIcon.SetActive(!isActive && !isBought);

        if (isActive)
            actionText.text = LeanLocalization.GetTranslationText("Selected", "Selected");
        else if (isBought)
            actionText.text = LeanLocalization.GetTranslationText("Select", "Select");
        else
            actionText.text = material.Price.ToString();
    }

    private void OnActionClicked()
    {
        if (materialController == null || materialController.IsMaterialActive(materialIndex))
            return;

        if (materialController.IsMaterialBought(materialIndex))
        {
            materialController.SelectMaterial(materialIndex);
            return;
        }

        if (!materialController.BuyMaterial(materialIndex))
        {
            MessageManager.Instance?.ShowMessage(new MessageData
            {
                Id = "PlanetSkins.PurchaseFailed",
                Owner = this,
                Text = LeanLocalization.GetTranslationText(
                    "Skin purchase failed", "Unable to purchase the skin. Check your diamond balance."),
                Type = MessageType.Toast,
                Channel = MessageChannel.Toast,
                DisplayDuration = 2f,
                Lifetime = 2f,
                CanDisplayOverModal = true,
                CanDisplayOverShop = true
            });
        }
    }
}
