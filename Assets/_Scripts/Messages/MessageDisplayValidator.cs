using System.Collections.Generic;
using UnityEngine;

namespace PlanetBuilder.Messages
{
    public sealed class MessageDisplayValidator
    {
        private readonly HashSet<object> _modalWindows = new();
        private bool _isModalWindowOpen;

        public bool IsAdvertisementOpen { get; private set; }
        public bool IsShopOpen { get; private set; }
        public bool IsModalWindowOpen => _isModalWindowOpen || _modalWindows.Count > 0;
        public string ModalBlockerSource { get; private set; }
        public bool IsTutorialRunning { get; private set; }
        public bool IsDialogOpen { get; private set; }

        public bool CanDisplay(MessageData message)
        {
            if (message == null ||
                IsAdvertisementOpen ||
                (IsShopOpen && !message.CanDisplayOverShop))
            {
                return false;
            }

            if (IsModalWindowOpen && !message.CanDisplayOverModal)
                return false;

            if (IsTutorialRunning)
                return message.Channel == MessageChannel.Tutorial;

            if (IsDialogOpen)
                return message.Channel == MessageChannel.Dialog;

            return true;
        }

        public void SetAdvertisementOpen(bool isOpen)
        {
            IsAdvertisementOpen = isOpen;
        }

        public void SetShopOpen(bool isOpen)
        {
            IsShopOpen = isOpen;
        }

        public void SetModalWindowOpen(bool isOpen)
        {
            _isModalWindowOpen = isOpen;
            RefreshModalBlockerSource();
        }

        public void RegisterModalWindow(object modalWindow)
        {
            if (modalWindow != null && _modalWindows.Add(modalWindow))
                RefreshModalBlockerSource();
        }

        public void UnregisterModalWindow(object modalWindow)
        {
            if (modalWindow != null && _modalWindows.Remove(modalWindow))
                RefreshModalBlockerSource();
        }

        public void SetTutorialRunning(bool isRunning)
        {
            IsTutorialRunning = isRunning;
        }

        public void SetDialogOpen(bool isOpen)
        {
            IsDialogOpen = isOpen;
        }

        private void RefreshModalBlockerSource()
        {
            if (_isModalWindowOpen)
            {
                ModalBlockerSource = "SetModalWindowOpen(true)";
                return;
            }

            foreach (object modalWindow in _modalWindows)
            {
                if (modalWindow is Component component && component != null)
                {
                    ModalBlockerSource =
                        $"{component.GetType().Name}:{component.gameObject.name} active={component.gameObject.activeInHierarchy}";
                    return;
                }

                if (modalWindow is GameObject gameObject && gameObject != null)
                {
                    ModalBlockerSource =
                        $"{gameObject.GetType().Name}:{gameObject.name} active={gameObject.activeInHierarchy}";
                    return;
                }

                ModalBlockerSource = modalWindow.GetType().Name;
                return;
            }

            ModalBlockerSource = null;
        }
    }
}
