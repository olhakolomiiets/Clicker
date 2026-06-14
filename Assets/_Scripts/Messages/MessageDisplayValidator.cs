using System.Collections.Generic;

namespace PlanetBuilder.Messages
{
    public sealed class MessageDisplayValidator
    {
        private readonly HashSet<object> _modalWindows = new();
        private bool _isModalWindowOpen;

        public bool IsAdvertisementOpen { get; private set; }
        public bool IsShopOpen { get; private set; }
        public bool IsModalWindowOpen => _isModalWindowOpen || _modalWindows.Count > 0;
        public bool IsTutorialRunning { get; private set; }
        public bool IsDialogOpen { get; private set; }

        public bool CanDisplay(MessageData message)
        {
            if (message == null ||
                IsAdvertisementOpen ||
                IsShopOpen)
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
        }

        public void RegisterModalWindow(object modalWindow)
        {
            if (modalWindow != null)
                _modalWindows.Add(modalWindow);
        }

        public void UnregisterModalWindow(object modalWindow)
        {
            if (modalWindow != null)
                _modalWindows.Remove(modalWindow);
        }

        public void SetTutorialRunning(bool isRunning)
        {
            IsTutorialRunning = isRunning;
        }

        public void SetDialogOpen(bool isOpen)
        {
            IsDialogOpen = isOpen;
        }
    }
}
