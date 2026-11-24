using System.Collections.Generic;
using Lean.Localization;
using PlanetBuilder.Loading;
using PlanetBuilder.Messages.Dialogs;
using PlanetBuilder.Messages.Tutorial;
using UnityEngine;
using UnityEngine.Video;

namespace PlanetBuilder.Messages
{
    [DisallowMultipleComponent]
    public sealed class PlanetCompletionController : MonoBehaviour
    {
        private const string FallbackSceneName = "PlanetMeta";
        [SerializeField] private string _targetSceneName = FallbackSceneName;

        [Header("Planet")]
        [SerializeField] private GameManager _gameManager;
        [SerializeField] private GameUI _gameUI;
        [SerializeField] private UIController _uiController;

        [Header("Completion Video")]
        [SerializeField] private MetaTutorialVideoView _videoView;
        [SerializeField] private VideoClip _videoClip;
        [SerializeField] private List<string> _dialogTextKeys = new();
        [SerializeField] private string _finalButtonTextKey = "planet_completion_start_new_project";

        private MessageManager _messageManager;
        private DialogManager _dialogManager;
        private TutorialManager _tutorialManager;
        private PlanetTutorialController _planetTutorialController;
        private MetaPlanetTutorialController _metaTutorialController;
        private PlanetLoadingScreen _loadingScreen;
        private DialogData _activeDialog;
        private string _dialogId;
        private string _confirmedTargetSceneName;
        private bool _showing;
        private bool _transitionRequested;

        private void OnEnable()
        {
            _dialogId = "PlanetCompletion." + gameObject.scene.name;
            _confirmedTargetSceneName = null;
            _transitionRequested = false;
        }

        private void OnDisable()
        {
            if (_dialogManager != null)
            {
                _dialogManager.OnDialogCompleted -= HandleDialogCompleted;
                _dialogManager.OnDialogCancelled -= HandleDialogCancelled;
            }
            StopPresentation();
            _messageManager = null;
            _dialogManager = null;
            _tutorialManager = null;
            _planetTutorialController = null;
            _metaTutorialController = null;
            _loadingScreen = null;
            _confirmedTargetSceneName = null;
        }

        private bool HasConfiguration()
        {
            if (!LastSceneLoader.TryGetSceneId(gameObject.scene.name, out int sceneId) || sceneId <= 0 ||
                _gameManager == null || _gameUI == null || _uiController == null ||
                _videoView == null || !_videoView.IsConfigured || _videoClip == null ||
                _dialogTextKeys == null || _dialogTextKeys.Count == 0 ||
                string.IsNullOrWhiteSpace(_finalButtonTextKey))
            {
                return false;
            }

            if (_gameManager.gameObject.scene != gameObject.scene ||
                _gameUI.gameObject.scene != gameObject.scene ||
                _uiController.gameObject.scene != gameObject.scene ||
                _videoView.gameObject.scene != gameObject.scene ||
                transform.IsChildOf(_videoView.transform))
            {
                return false;
            }

            for (int i = 0; i < _dialogTextKeys.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(_dialogTextKeys[i]))
                    return false;
            }
            return true;
        }

        private bool ResolveInfrastructure()
        {
            if (_messageManager == null)
                _messageManager = MessageManager.Instance;
            if (_messageManager == null)
                return false;
            if (_dialogManager == null)
            {
                _dialogManager = _messageManager.GetComponent<DialogManager>();
                if (_dialogManager != null)
                {
                    _dialogManager.OnDialogCompleted += HandleDialogCompleted;
                    _dialogManager.OnDialogCancelled += HandleDialogCancelled;
                }
            }
            if (_tutorialManager == null)
                _tutorialManager = _messageManager.GetComponent<TutorialManager>();
            if (_planetTutorialController == null)
                _planetTutorialController = FindFirstObjectByType<PlanetTutorialController>();
            if (_metaTutorialController == null)
                _metaTutorialController = FindFirstObjectByType<MetaPlanetTutorialController>();
            if (_loadingScreen == null)
                _loadingScreen = PlanetLoadingScreen.Instance;
            return _dialogManager != null && _dialogManager.isActiveAndEnabled &&
                _tutorialManager != null && _loadingScreen != null;
        }

        private bool IsTutorialActive() =>
            _tutorialManager.IsRunning || _messageManager.DisplayValidator.IsTutorialRunning ||
            (_planetTutorialController != null && _planetTutorialController.isActiveAndEnabled &&
             PlanetTutorialController.ShouldStartTutorial()) ||
            (_metaTutorialController != null && _metaTutorialController.isActiveAndEnabled &&
             MetaPlanetTutorialController.ShouldStartTutorial());

        public void ShowCompletionVideo()
        {
            if (!isActiveAndEnabled || _showing || _transitionRequested)
                return;
            if (!HasConfiguration())
            {
                Debug.LogWarning("[PlanetCompletion] Assign the scene references, video and dialogue keys.", this);
                return;
            }
            if (!CanStart())
                return;

            _confirmedTargetSceneName = null;
            TryShow();
        }

        private bool CanStart()
        {
            if (!isActiveAndEnabled || _showing || _transitionRequested ||
                !_gameManager.IsPlanetCompletionReady || !ResolveInfrastructure() || IsTutorialActive() ||
                Time.timeScale <= 0f || _loadingScreen.IsShowing || _videoView == null || !_videoView.IsConfigured ||
                _videoView.gameObject.activeSelf ||
                (_videoView.transform.parent != null && !_videoView.transform.parent.gameObject.activeInHierarchy) ||
                _dialogManager.IsRunning || _messageManager.DisplayValidator.IsDialogOpen ||
                _messageManager.DisplayValidator.IsAdvertisementOpen ||
                _messageManager.GetCurrentMessage(MessageChannel.Dialog) != null ||
                _messageManager.GetCurrentMessage(MessageChannel.Tutorial) != null)
            {
                return false;
            }
            return true;
        }

        private bool TryShow()
        {
            if (string.IsNullOrWhiteSpace(LeanLocalization.GetTranslationText(_finalButtonTextKey, null)))
                return false;
            for (int i = 0; i < _dialogTextKeys.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(LeanLocalization.GetTranslationText(_dialogTextKeys[i], null)))
                    return false;
            }

            if (_uiController.IsShopDisplayed)
                _uiController.ToggleUpgradeStorePanel();
            if (_uiController.isLevelsDisplayed)
                _uiController.ToggleLevelsPanel();
            if (!CanStart() || _messageManager.DisplayValidator.IsModalWindowOpen)
                return false;

            _activeDialog = new DialogData
            {
                DialogId = _dialogId,
                Owner = this,
                VideoView = _videoView.DialogView
            };
            for (int i = 0; i < _dialogTextKeys.Count; i++)
            {
                _activeDialog.ListDialogLine.Add(new DialogLine
                {
                    Text = LeanLocalization.GetTranslationText(_dialogTextKeys[i], null)
                });
            }

            _showing = true;
            _videoView.DialogView.SetPortraitHidden(true);
            _videoView.Show(_videoClip, _dialogId, _finalButtonTextKey,
                canSkip: false, onVideoCompleted: null, onCompleted: HandleConfirmed, loopUntilConfirmed: true);
            _videoView.SetConfirmationHandler(TryConfirm);
            if (_videoView.isActiveAndEnabled && _dialogManager.StartDialog(_activeDialog) && _showing)
                return true;

            StopPresentation();
            return false;
        }

        private void HandleDialogCompleted(DialogData dialog)
        {
            if (!_showing || !ReferenceEquals(dialog, _activeDialog))
                return;
            _activeDialog = null;
            _videoView.SetDialogCompleted();
        }

        private void HandleDialogCancelled(DialogData dialog)
        {
            if (!ReferenceEquals(dialog, _activeDialog))
                return;
            StopPresentation();
        }

        private bool TryConfirm()
        {
            if (!_showing || _activeDialog != null || !_gameManager.IsPlanetCompletionReady ||
                Time.timeScale <= 0f || _messageManager == null ||
                _loadingScreen == null || _loadingScreen.IsShowing || IsTutorialActive() ||
                _messageManager.DisplayValidator.IsAdvertisementOpen)
            {
                return false;
            }

            if (!TryResolveTargetScene(out string targetSceneName))
                return false;
            if (!_gameManager.TryConfirmPlanetCompletion())
                return false;

            _confirmedTargetSceneName = targetSceneName;
            return true;
        }

        private bool TryResolveTargetScene(out string sceneName)
        {
            sceneName = null;
            if (string.IsNullOrWhiteSpace(_targetSceneName) ||
                !Application.CanStreamedLevelBeLoaded(_targetSceneName) ||
                !LastSceneLoader.TryGetSceneId(_targetSceneName, out _))
            {
                return false;
            }

            string targetSceneName = _gameManager.CanAccessPlanetScene(_targetSceneName)
                ? _targetSceneName
                : FallbackSceneName;
            if (!Application.CanStreamedLevelBeLoaded(targetSceneName) ||
                !_gameManager.CanAccessPlanetScene(targetSceneName))
            {
                return false;
            }

            sceneName = targetSceneName;
            return true;
        }

        private void HandleConfirmed()
        {
            _showing = false;
            _transitionRequested = true;
            _loadingScreen.LoadBuiltInScene(_confirmedTargetSceneName);
        }

        private void StopPresentation()
        {
            bool wasShowing = _showing;
            _showing = false;
            DialogData dialog = _activeDialog;
            _activeDialog = null;
            if (_dialogManager != null && dialog != null && ReferenceEquals(_dialogManager.ActiveDialog, dialog))
                _dialogManager.CancelDialog();
            if (wasShowing && _videoView != null)
            {
                _videoView.SetConfirmationHandler(null);
                _videoView.gameObject.SetActive(false);
            }
        }
    }
}
