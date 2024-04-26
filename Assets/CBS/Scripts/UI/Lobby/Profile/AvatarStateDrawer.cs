using CBS.Core;
using CBS.Models;
using CBS.Utils;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class AvatarStateDrawer : MonoBehaviour, IScrollableItem<CBSAvatarState>
    {
        [Header("States")]
        [SerializeField]
        private GameObject LockState;
        [SerializeField]
        private GameObject AvailableState;
        [SerializeField]
        private GameObject SelectedState;
        [SerializeField]
        private GameObject PurchaseState;

        [SerializeField]
        private Image Avatar;
        [SerializeField]
        private Text LockLabel;
        [SerializeField]
        private PurchaseButton PurchaseButton;

        private Action<CBSAvatarState> SelectionAction { get; set; }
        private Action<CBSAvatarState> PurchaseAction { get; set; }
        private CBSAvatarState AvatarState { get; set; }

        public void Display(CBSAvatarState data)
        {
            AvatarState = data;
            ClearAllState();
            Avatar.sprite = data.GetSprite();
            var available = data.IsAvailable;
            if (available)
            {
                AvailableState.SetActive(!data.Selected);
                SelectedState.SetActive(data.Selected);
            }
            else
            {
                if (data.Purchasable && !data.Purchased)
                {
                    PurchaseState.SetActive(true);
                    var price = data.Price;
                    PurchaseButton.Display(price.CurrencyID, price.CurrencyValue, null);
                }
                else if (data.LockedByLevel && data.HasLevelLimit)
                {
                    LockState.SetActive(true);
                    LockLabel.text = ProfileTXTHandler.GetAvatarLockText(data.LevelLimit);
                }
            }
        }

        public void SetCallbacks(Action<CBSAvatarState> selectAction, Action<CBSAvatarState> purchaseAction)
        {
            SelectionAction = selectAction;
            PurchaseAction = purchaseAction;
        }

        private void ClearAllState()
        {
            LockState.SetActive(false);
            AvailableState.SetActive(false);
            SelectedState.SetActive(false);
            PurchaseState.SetActive(false);
        }

        // buttons click
        public void PurchaseClick()
        {
            PurchaseAction?.Invoke(AvatarState);
        }

        public void SelectClick()
        {
            SelectionAction?.Invoke(AvatarState);
        }
    }
}
