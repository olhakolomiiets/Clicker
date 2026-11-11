using TMPro;
using UnityEngine;
using PlanetBuilder.Messages;

namespace PlanetBuilder.Messages.UI
{
    public abstract class BaseMessageView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private CharacterPortraitController _portraitController;

        [Header("Top Placement")]
        [Tooltip("Minimum and maximum Y anchors, respectively. X anchors come from the normal layout.")]
        [SerializeField] private Vector2 _topVerticalAnchors = new(0.75f, 0.95f);
        [SerializeField] private Vector2 _topAnchoredPosition = Vector2.zero;
        [Tooltip("Added to the normal layout's Size Delta.")]
        [SerializeField] private Vector2 _topSizeDeltaOffset = Vector2.zero;

        private RectTransform _rectTransform;
        private Vector2 _defaultAnchorMin;
        private Vector2 _defaultAnchorMax;
        private Vector2 _defaultAnchoredPosition;
        private Vector2 _defaultSizeDelta;
        private bool _hasDefaultPlacement;

        protected TMP_Text MessageText => _messageText;

        internal MessageData DisplayedMessage { get; private set; }

        internal void SetPortraitHidden(bool hidden)
        {
            if (_portraitController != null)
                _portraitController.SetTemporarilyHidden(hidden);
        }

        public void SetTopPlacement(bool useTopPlacement, string stepId = null, string reason = null)
        {
            EnsureDefaultPlacement();

            if (_rectTransform == null)
                return;

            string mode = useTopPlacement ? "top" : "default";
            string before = GetRectDescription();
            Debug.Log(
                $"[MetaTutorialTrace] Dialog position mode requested: {mode}\n" +
                $"Dialog position step id: {stepId ?? "null"}\n" +
                $"Dialog position reason: {reason ?? "null"}\n" +
                $"Dialog rect before: {before}",
                this);

            if (useTopPlacement)
            {
                _rectTransform.anchorMin = new Vector2(_defaultAnchorMin.x, _topVerticalAnchors.x);
                _rectTransform.anchorMax = new Vector2(_defaultAnchorMax.x, _topVerticalAnchors.y);
                _rectTransform.anchoredPosition = _topAnchoredPosition;
                _rectTransform.sizeDelta = _defaultSizeDelta + _topSizeDeltaOffset;
                Debug.Log($"[MetaTutorialTrace] Dialog rect after: {GetRectDescription()}", this);
                return;
            }

            _rectTransform.anchorMin = _defaultAnchorMin;
            _rectTransform.anchorMax = _defaultAnchorMax;
            _rectTransform.anchoredPosition = _defaultAnchoredPosition;
            _rectTransform.sizeDelta = _defaultSizeDelta;
            Debug.Log($"[MetaTutorialTrace] Dialog rect after: {GetRectDescription()}", this);
        }

        public virtual void Show(MessageData data)
        {
            if (data == null)
                return;

            DisplayedMessage = data;

            if (_messageText != null)
                _messageText.text = data.Text;

            if (_portraitController != null)
                _portraitController.ShowCharacter(data.SpeakerId, data.CharacterMood);

            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            MessageTutorialTrace.LogHide(
                $"{GetType().Name}.Hide/BaseMessageView.Hide",
                gameObject,
                DisplayedMessage != null
                    ? $"messageId={DisplayedMessage.Id} type={DisplayedMessage.Type} channel={DisplayedMessage.Channel}"
                    : "message=null");

            DisplayedMessage = null;

            if (_portraitController != null)
                _portraitController.Hide();

            gameObject.SetActive(false);
        }

        private void EnsureDefaultPlacement()
        {
            if (_hasDefaultPlacement)
                return;

            _rectTransform = transform as RectTransform;

            if (_rectTransform == null)
                return;

            _defaultAnchorMin = _rectTransform.anchorMin;
            _defaultAnchorMax = _rectTransform.anchorMax;
            _defaultAnchoredPosition = _rectTransform.anchoredPosition;
            _defaultSizeDelta = _rectTransform.sizeDelta;
            _hasDefaultPlacement = true;
        }

        private string GetRectDescription()
        {
            if (_rectTransform == null)
                return "null";

            return $"anchorMin={_rectTransform.anchorMin} anchorMax={_rectTransform.anchorMax} " +
                   $"pivot={_rectTransform.pivot} anchoredPosition={_rectTransform.anchoredPosition} " +
                   $"sizeDelta={_rectTransform.sizeDelta}";
        }
    }
}
