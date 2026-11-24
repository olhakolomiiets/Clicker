using System;
using PlanetBuilder.Messages;
using PlanetBuilder.Messages.Dialogs;
using PlanetBuilder.Messages.UI;
using Lean.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PlanetBuilder.Messages.Tutorial
{
    public class MetaTutorialVideoView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private VideoPlayer _videoPlayer;
        [SerializeField] private Button _skipButton;
        [SerializeField] private TMP_Text _skipButtonText;
        [SerializeField] private string _continueTextKey = "meta_tutorial_video_continue";
        [SerializeField] private string _skipTextKey = "meta_tutorial_video_skip";
        [SerializeField] private VideoDialogView _videoDialogView;

        private const string StartWorkTextKey = "meta_tutorial_step11_contract_button";

        private VideoClip _inspectorVideoClip;
        private bool _hasInspectorVideoClip;
        private string _dialogId;
        private string _finalButtonTextKey;
        private MessageManager _modalMessageManager;
        private Action _onVideoCompleted;
        private Action _onCompleted;
        private Func<bool> _tryConfirm;
        private bool _canSkip;
        private bool _loopUntilConfirmed;
        private bool _videoCompleted;
        private bool _dialogCompleted;

        internal VideoDialogView DialogView => _videoDialogView;
        internal bool IsConfigured => enabled && _videoPlayer != null && _videoPlayer.enabled &&
            _skipButton != null && _skipButtonText != null &&
            _videoDialogView != null && _videoDialogView.enabled;

        internal void SetConfirmationHandler(Func<bool> tryConfirm)
        {
            _tryConfirm = tryConfirm;
        }

        private void OnEnable()
        {
            if (_skipButton != null)
                _skipButton.onClick.AddListener(HandleButton);

            if (_videoPlayer != null)
                _videoPlayer.loopPointReached += HandleVideoFinished;

            SetModalOpen(true);
        }

        private void OnDisable()
        {
            MessageTutorialTrace.LogHide(
                "MetaTutorialVideoView.OnDisable",
                gameObject,
                $"canSkip={_canSkip} videoCompleted={_videoCompleted} dialogCompleted={_dialogCompleted}");

            if (_skipButton != null)
                _skipButton.onClick.RemoveListener(HandleButton);

            if (_videoPlayer != null)
            {
                _videoPlayer.loopPointReached -= HandleVideoFinished;
                _videoPlayer.Stop();
                if (_hasInspectorVideoClip)
                    _videoPlayer.clip = _inspectorVideoClip;
            }

            if (_videoDialogView != null && _videoDialogView.gameObject.activeSelf)
                _videoDialogView.Hide();

            SetModalOpen(false);
            _onVideoCompleted = null;
            _onCompleted = null;
            _tryConfirm = null;
            _loopUntilConfirmed = false;
            _dialogId = null;
            _finalButtonTextKey = null;
        }

        public void Show(bool canSkip, Action onVideoCompleted, Action onCompleted)
        {
            CacheInspectorVideoClip();
            Show(_inspectorVideoClip, "MetaPlanetTutorial.FirstOrder", StartWorkTextKey,
                canSkip, onVideoCompleted, onCompleted);
        }

        public void Show(
            VideoClip videoClip,
            string dialogId,
            string finalButtonTextKey,
            bool canSkip,
            Action onVideoCompleted,
            Action onCompleted)
        {
            Show(videoClip, dialogId, finalButtonTextKey, canSkip,
                onVideoCompleted, onCompleted, loopUntilConfirmed: false);
        }

        public void Show(
            VideoClip videoClip,
            string dialogId,
            string finalButtonTextKey,
            bool canSkip,
            Action onVideoCompleted,
            Action onCompleted,
            bool loopUntilConfirmed)
        {
            if (string.IsNullOrEmpty(dialogId) || string.IsNullOrEmpty(finalButtonTextKey))
            {
                Debug.LogWarning("Video view requires a dialog ID and a final button localization key.", this);
                return;
            }

            CacheInspectorVideoClip();
            _dialogId = dialogId;
            _finalButtonTextKey = finalButtonTextKey;
            _canSkip = canSkip;
            _loopUntilConfirmed = loopUntilConfirmed;
            _tryConfirm = null;
            _videoCompleted = false;
            _dialogCompleted = false;
            _onVideoCompleted = onVideoCompleted;
            _onCompleted = onCompleted;

            if (_videoPlayer != null)
            {
                _videoPlayer.Stop();
                _videoPlayer.clip = videoClip;
                _videoPlayer.isLooping = _loopUntilConfirmed;
            }

            gameObject.SetActive(true);
            RefreshButton();

            if (_videoPlayer != null && _videoPlayer.clip != null)
            {
                _videoPlayer.Play();
            }
            else
                HandleVideoFinished(_videoPlayer);
        }

        private void CacheInspectorVideoClip()
        {
            if (_hasInspectorVideoClip || _videoPlayer == null)
                return;

            _inspectorVideoClip = _videoPlayer.clip;
            _hasInspectorVideoClip = true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_dialogCompleted || string.IsNullOrEmpty(_dialogId))
                return;

            MessageManager messageManager = MessageManager.Instance;
            DialogManager dialogManager = messageManager != null
                ? messageManager.GetComponent<DialogManager>()
                : null;

            DialogData activeDialog = dialogManager != null ? dialogManager.ActiveDialog : null;
            if (_videoDialogView != null && activeDialog != null &&
                activeDialog.DialogId == _dialogId && activeDialog.VideoView == _videoDialogView)
            {
                dialogManager.ShowNext();
            }
        }

        public void SetDialogCompleted()
        {
            if (!isActiveAndEnabled)
                return;

            _dialogCompleted = true;
            RefreshButton();
        }

        private void HandleButton()
        {
            if (!_videoCompleted)
            {
                if (_canSkip)
                    CompleteVideo();

                return;
            }

            if (_dialogCompleted)
                Complete();
        }

        private void HandleVideoFinished(VideoPlayer source)
        {
            CompleteVideo();
        }

        private void CompleteVideo()
        {
            if (_videoCompleted)
                return;

            _videoCompleted = true;

            if (_videoPlayer != null && !_loopUntilConfirmed)
                _videoPlayer.Pause();

            Action videoCompleted = _onVideoCompleted;
            _onVideoCompleted = null;
            videoCompleted?.Invoke();
            if (isActiveAndEnabled)
                RefreshButton();
        }

        private void RefreshButton()
        {
            if (_skipButton == null)
                return;

            bool buttonVisible = (!_videoCompleted && _canSkip) ||
                                 (_videoCompleted && _dialogCompleted);
            _skipButton.gameObject.SetActive(buttonVisible);

            if (_skipButtonText != null)
                _skipButtonText.text = T(_videoCompleted ? _finalButtonTextKey : _skipTextKey);
        }

        private static string T(string key)
        {
            return LeanLocalization.GetTranslationText(key, key);
        }

        private void Complete()
        {
            if (!_videoCompleted || !_dialogCompleted)
                return;

            if (_tryConfirm != null && !_tryConfirm())
                return;

            Action completed = _onCompleted;
            MessageTutorialTrace.LogHide(
                "MetaTutorialVideoView.Complete",
                gameObject,
                $"hasCompletedCallback={completed != null}");
            gameObject.SetActive(false);
            completed?.Invoke();
        }

        private void SetModalOpen(bool isOpen)
        {
            MessageManager messageManager = isOpen ? MessageManager.Instance : _modalMessageManager;
            if (messageManager == null)
                return;

            if (isOpen)
            {
                _modalMessageManager = messageManager;
                messageManager.RegisterModalWindow(this);
            }
            else
            {
                _modalMessageManager = null;
                messageManager.UnregisterModalWindow(this);
            }

            messageManager.SetConversationPortraitsHidden(isOpen, this);
        }
    }
}
