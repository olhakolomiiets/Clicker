using PlanetBuilder.Messages;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlanetBuilder.Messages.UI
{
    public abstract class TypewriterMessageView : BaseMessageView, IPointerClickHandler
    {
        private const float DefaultTypingSpeed = 30f;

        private float _visibleCharacterProgress;
        private int _characterCount;
        private bool _isTyping;

        public bool IsTyping => _isTyping;

        public override void Show(MessageData data)
        {
            CompleteTyping();
            base.Show(data);

            if (data == null || MessageText == null)
                return;

            MessageText.ForceMeshUpdate();
            _characterCount = MessageText.textInfo.characterCount;

            if (!data.UseTypingAnimation || _characterCount == 0)
            {
                CompleteTyping();
                return;
            }

            _visibleCharacterProgress = 0f;
            _isTyping = true;
            MessageText.maxVisibleCharacters = 0;
        }

        public override void Hide()
        {
            MessageTutorialTrace.LogHide(
                $"{GetType().Name}.Hide/TypewriterMessageView.Hide",
                gameObject,
                DisplayedMessage != null
                    ? $"messageId={DisplayedMessage.Id} type={DisplayedMessage.Type} channel={DisplayedMessage.Channel}"
                    : "message=null");

            CompleteTyping();
            base.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            HandleClick();
        }

        public void HandleClick()
        {
            if (DisplayedMessage == null)
                return;

            if (_isTyping)
            {
                if (DisplayedMessage.CanSkipTyping)
                    CompleteTyping();

                return;
            }

            MessageManager manager = MessageManager.Instance;

            if (manager != null)
                manager.AdvanceCurrentMessage(DisplayedMessage.Channel);
        }

        private void Update()
        {
            if (!_isTyping || DisplayedMessage == null || MessageText == null)
                return;

            float typingSpeed = DisplayedMessage.TypingSpeed > 0f
                ? DisplayedMessage.TypingSpeed
                : DefaultTypingSpeed;

            _visibleCharacterProgress += typingSpeed * Time.unscaledDeltaTime;
            int visibleCharacterCount = Mathf.Min((int)_visibleCharacterProgress, _characterCount);
            MessageText.maxVisibleCharacters = visibleCharacterCount;

            if (visibleCharacterCount >= _characterCount)
                CompleteTyping();
        }

        private void CompleteTyping()
        {
            _isTyping = false;
            _visibleCharacterProgress = 0f;

            if (MessageText != null)
                MessageText.maxVisibleCharacters = int.MaxValue;
        }
    }
}
