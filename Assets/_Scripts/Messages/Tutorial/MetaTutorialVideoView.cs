using System;
using PlanetBuilder.Messages;
using Lean.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PlanetBuilder.Messages.Tutorial
{
    public class MetaTutorialVideoView : MonoBehaviour
    {
        [SerializeField] private VideoPlayer _videoPlayer;
        [SerializeField] private Button _skipButton;
        [SerializeField] private TMP_Text _skipButtonText;
        [SerializeField] private string _continueTextKey = "meta_tutorial_video_continue";
        [SerializeField] private string _skipTextKey = "meta_tutorial_video_skip";

        private const string StartWorkTextKey = "meta_tutorial_step11_contract_button";

        private Action _onVideoCompleted;
        private Action _onCompleted;
        private bool _canSkip;
        private bool _videoCompleted;
        private bool _dialogCompleted;

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
            }

            SetModalOpen(false);
            _onVideoCompleted = null;
            _onCompleted = null;
        }

        public void Show(bool canSkip, Action onVideoCompleted, Action onCompleted)
        {
            _canSkip = canSkip;
            _videoCompleted = false;
            _dialogCompleted = false;
            _onVideoCompleted = onVideoCompleted;
            _onCompleted = onCompleted;
            gameObject.SetActive(true);
            RefreshButton();

            if (_videoPlayer != null && _videoPlayer.clip != null)
            {
                _videoPlayer.isLooping = false;
                _videoPlayer.Play();
            }
            else
                HandleVideoFinished(_videoPlayer);
        }

        public void SetDialogCompleted()
        {
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

            if (_videoPlayer != null)
                _videoPlayer.Pause();

            _onVideoCompleted?.Invoke();
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
                _skipButtonText.text = T(_videoCompleted ? StartWorkTextKey : _skipTextKey);
        }

        private static string T(string key)
        {
            return LeanLocalization.GetTranslationText(key, key);
        }

        private void Complete()
        {
            if (!_videoCompleted || !_dialogCompleted)
                return;

            Action completed = _onCompleted;
            MessageTutorialTrace.LogHide(
                "MetaTutorialVideoView.Complete",
                gameObject,
                $"hasCompletedCallback={completed != null}");
            gameObject.SetActive(false);
            completed?.Invoke();
        }

        private static void SetModalOpen(bool isOpen)
        {
            MessageManager messageManager = MessageManager.Instance;

            if (messageManager != null)
                messageManager.SetModalWindowOpen(isOpen);
        }
    }
}
