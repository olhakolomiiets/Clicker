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

        private Action _onCompleted;
        private bool _canSkip;
        private bool _isFinished;

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
                $"canSkip={_canSkip} isFinished={_isFinished}");

            if (_skipButton != null)
                _skipButton.onClick.RemoveListener(HandleButton);

            if (_videoPlayer != null)
            {
                _videoPlayer.loopPointReached -= HandleVideoFinished;
                _videoPlayer.Stop();
            }

            SetModalOpen(false);
            _onCompleted = null;
        }

        public void Show(bool canSkip, Action onCompleted)
        {
            _canSkip = canSkip;
            _isFinished = false;
            _onCompleted = onCompleted;
            gameObject.SetActive(true);
            RefreshButton();

            if (_videoPlayer != null && _videoPlayer.clip != null)
                _videoPlayer.Play();
            else
                HandleVideoFinished(_videoPlayer);
        }

        private void HandleButton()
        {
            if (!_canSkip && !_isFinished)
                return;

            Complete();
        }

        private void HandleVideoFinished(VideoPlayer source)
        {
            _isFinished = true;
            RefreshButton();
        }

        private void RefreshButton()
        {
            if (_skipButton == null)
                return;

            bool buttonVisible = _canSkip || _isFinished;
            _skipButton.gameObject.SetActive(buttonVisible);

            if (_skipButtonText != null)
                _skipButtonText.text = T(_isFinished ? _continueTextKey : _skipTextKey);
        }

        private static string T(string key)
        {
            return LeanLocalization.GetTranslationText(key, key);
        }

        private void Complete()
        {
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
