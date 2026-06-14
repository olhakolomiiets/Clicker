using UnityEngine;

namespace PlanetBuilder.Messages
{
    public sealed class MessageDisplayModalBlocker : MonoBehaviour
    {
        private MessageManager _messageManager;

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void TryRegister()
        {
            if (_messageManager != null)
                return;

            _messageManager = MessageManager.Instance;

            if (_messageManager != null)
                _messageManager.RegisterModalWindow(this);
        }

        private void OnDisable()
        {
            if (_messageManager != null)
                _messageManager.UnregisterModalWindow(this);

            _messageManager = null;
        }
    }
}
