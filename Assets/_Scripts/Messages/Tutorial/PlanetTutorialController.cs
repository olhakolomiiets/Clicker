using PlanetBuilder.Messages.Dialogs;
using UnityEngine;

namespace PlanetBuilder.Messages.Tutorial
{
    public class PlanetTutorialController : MonoBehaviour
    {
        private const string CompletedKey = "PlanetTutorialCompleted";
        private const string CurrentStageKey = "PlanetTutorialCurrentStage";
        private const string CurrentStepIdKey = "PlanetTutorialCurrentStepId";
        private const string FirstVisitShownKey = "PlanetTutorialFirstVisitShown";
        private const string CoinsEarnedFromSmallTreeKey = "PlanetTutorialCoinsEarnedFromSmallTree";
        private const string SmallTreeBoughtKey = "PlanetTutorialSmallTreeBought";
        private const string BigTreeBoughtKey = "PlanetTutorialBigTreeBought";
        private const string SmallTreeManagerBoughtKey = "PlanetTutorialSmallTreeManagerBought";

        private const int SmallTreeIndex = 0;
        private const int BigTreeIndex = 1;

        public enum PlanetTutorialStage
        {
            FirstVisit = 0,
            OpenCreationTab = 1,
            EarnCoinsSmallTree = 2,
            BuySmallTree = 3,
            BuyBigTree = 4,
            BuySmallTreeManager = 5
        }

        [Header("Tutorial")]
        [SerializeField] private TutorialManager _tutorialManager;
        [SerializeField] private DialogManager _dialogManager;
        [SerializeField] private TutorialInteractionOverlay _interactionOverlay;
        [SerializeField] private TutorialHighlightSystem _highlightSystem;
        [SerializeField] private bool _startAutomatically = true;

        [Header("Planet")]
        [SerializeField] private UIController _uiController;
        [SerializeField] private GameRules _gameRules;
        [SerializeField] private GameUI _gameUI;
        [SerializeField] private GameManager _gameManager;

        [Header("Legacy Tutorial Optional")]
        [SerializeField] private global::TutorialManager _legacyTutorialManager;

        private PlanetTutorialStage _currentStage;
        private bool _isInitialized;
        private bool _isRunning;

        private void Start()
        {
            Initialize();

            if (_startAutomatically)
                RunSavedStage();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        public void Initialize()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            _currentStage = LoadStage();
            Debug.Log($"[PlanetTutorialTrace] PlanetTutorial initialized stage={_currentStage} completed={IsCompleted()}", this);

            if (_legacyTutorialManager != null)
                Debug.Log("[PlanetTutorialTrace] legacy TutorialManager reference is set but not disabled in Phase 1", this);
        }

        public void RunSavedStage()
        {
            Initialize();

            if (IsCompleted())
            {
                Debug.Log("[PlanetTutorialTrace] run skipped because tutorial is completed", this);
                return;
            }

            if (!HasRequiredReferences())
            {
                Debug.LogWarning("[PlanetTutorialTrace] run skipped because required references are missing", this);
                return;
            }

            _isRunning = true;
            _currentStage = LoadStage();
            Debug.Log($"[PlanetTutorialTrace] stage loaded stage={_currentStage}", this);
            StartStage(_currentStage);
        }

        public void AdvanceToStage(PlanetTutorialStage stage)
        {
            Initialize();

            if (IsCompleted())
                return;

            _currentStage = stage;
            SaveStage(stage);
            Debug.Log($"[PlanetTutorialTrace] stage completed, advancing to stage={stage}", this);
            StartStage(stage);
        }

        public void CompleteTutorial()
        {
            PlayerPrefs.SetInt(CompletedKey, 1);
            PlayerPrefs.DeleteKey(CurrentStepIdKey);
            PlayerPrefs.Save();

            _isRunning = false;

            if (_tutorialManager != null)
                _tutorialManager.CancelTutorial();

            Debug.Log("[PlanetTutorialTrace] tutorial completed", this);
        }

        private void StartStage(PlanetTutorialStage stage)
        {
            SaveStage(stage);
            Debug.Log($"[PlanetTutorialTrace] stage started stage={stage}", this);

            switch (stage)
            {
                case PlanetTutorialStage.FirstVisit:
                    StartFirstVisit();
                    break;
                case PlanetTutorialStage.OpenCreationTab:
                    StartOpenCreationTab();
                    break;
                case PlanetTutorialStage.EarnCoinsSmallTree:
                    StartEarnCoinsSmallTree();
                    break;
                case PlanetTutorialStage.BuySmallTree:
                    StartBuySmallTree();
                    break;
                case PlanetTutorialStage.BuyBigTree:
                    StartBuyBigTree();
                    break;
                case PlanetTutorialStage.BuySmallTreeManager:
                    StartBuySmallTreeManager();
                    break;
                default:
                    Debug.LogWarning($"[PlanetTutorialTrace] unknown stage={stage}", this);
                    break;
            }
        }

        private void StartFirstVisit()
        {
            Debug.Log("[PlanetTutorialTrace] FirstVisit skeleton ready", this);
        }

        private void StartOpenCreationTab()
        {
            GameObject target = _uiController != null ? _uiController.ToggleButtonTarget : null;
            Debug.Log($"[PlanetTutorialTrace] OpenCreationTab skeleton ready target={GetObjectName(target)}", this);
        }

        private void StartEarnCoinsSmallTree()
        {
            GameObject target = _gameUI != null ? _gameUI.GetCreationProgressButtonTarget(SmallTreeIndex) : null;
            Debug.Log($"[PlanetTutorialTrace] EarnCoinsSmallTree skeleton ready target={GetObjectName(target)}", this);
        }

        private void StartBuySmallTree()
        {
            GameObject target = _gameUI != null ? _gameUI.GetCreationBuyButtonTarget(SmallTreeIndex) : null;
            Debug.Log($"[PlanetTutorialTrace] BuySmallTree skeleton ready target={GetObjectName(target)}", this);
        }

        private void StartBuyBigTree()
        {
            GameObject target = _gameUI != null ? _gameUI.GetCreationBuyButtonTarget(BigTreeIndex) : null;
            Debug.Log($"[PlanetTutorialTrace] BuyBigTree skeleton ready target={GetObjectName(target)}", this);
        }

        private void StartBuySmallTreeManager()
        {
            GameObject managerTarget = _gameUI != null ? _gameUI.GetManagerButtonTarget(SmallTreeIndex) : null;
            GameObject buyTarget = _gameUI != null ? _gameUI.GetManagerBuyButtonTarget(SmallTreeIndex) : null;
            Debug.Log(
                $"[PlanetTutorialTrace] BuySmallTreeManager skeleton ready managerTarget={GetObjectName(managerTarget)} buyTarget={GetObjectName(buyTarget)}",
                this);
        }

        private void SubscribeEvents()
        {
            if (_uiController != null)
            {
                _uiController.OnShopOpened += HandleShopOpened;
                _uiController.OnShopTabSelected += HandleShopTabSelected;
            }

            if (_gameRules != null)
            {
                _gameRules.OnItemIncomeEarned += HandleItemIncomeEarned;
                _gameRules.OnItemFirstPurchaseConfirmed += HandleItemFirstPurchaseConfirmed;
                _gameRules.OnItemUpgradedConfirmed += HandleItemUpgradedConfirmed;
                _gameRules.OnManagerPurchasedConfirmed += HandleManagerPurchasedConfirmed;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_uiController != null)
            {
                _uiController.OnShopOpened -= HandleShopOpened;
                _uiController.OnShopTabSelected -= HandleShopTabSelected;
            }

            if (_gameRules != null)
            {
                _gameRules.OnItemIncomeEarned -= HandleItemIncomeEarned;
                _gameRules.OnItemFirstPurchaseConfirmed -= HandleItemFirstPurchaseConfirmed;
                _gameRules.OnItemUpgradedConfirmed -= HandleItemUpgradedConfirmed;
                _gameRules.OnManagerPurchasedConfirmed -= HandleManagerPurchasedConfirmed;
            }
        }

        private void HandleShopOpened()
        {
            Debug.Log("[PlanetTutorialTrace] shop opened confirmed", this);
        }

        private void HandleShopTabSelected(int index)
        {
            Debug.Log($"[PlanetTutorialTrace] shop tab selected index={index}", this);
        }

        private void HandleItemIncomeEarned(int index, double income)
        {
            Debug.Log($"[PlanetTutorialTrace] income confirmed index={index} income={income} currentMoney={GetCurrentMoney()}", this);

            if (index == SmallTreeIndex && income > 0d)
                SaveFlag(CoinsEarnedFromSmallTreeKey);
        }

        private void HandleItemFirstPurchaseConfirmed(int index)
        {
            Debug.Log($"[PlanetTutorialTrace] first purchase confirmed index={index}", this);

            if (index == BigTreeIndex)
                SaveFlag(BigTreeBoughtKey);
        }

        private void HandleItemUpgradedConfirmed(int index)
        {
            Debug.Log($"[PlanetTutorialTrace] item upgrade confirmed index={index}", this);

            if (index == SmallTreeIndex)
                SaveFlag(SmallTreeBoughtKey);
        }

        private void HandleManagerPurchasedConfirmed(int index)
        {
            Debug.Log($"[PlanetTutorialTrace] manager purchase confirmed index={index}", this);

            if (index == SmallTreeIndex)
                SaveFlag(SmallTreeManagerBoughtKey);
        }

        private bool HasRequiredReferences()
        {
            bool hasReferences = true;

            if (_tutorialManager == null)
            {
                Debug.LogWarning("[PlanetTutorialTrace] missing TutorialManager reference", this);
                hasReferences = false;
            }

            if (_dialogManager == null)
            {
                Debug.LogWarning("[PlanetTutorialTrace] missing DialogManager reference", this);
                hasReferences = false;
            }

            if (_uiController == null)
            {
                Debug.LogWarning("[PlanetTutorialTrace] missing UIController reference", this);
                hasReferences = false;
            }

            if (_gameRules == null)
            {
                Debug.LogWarning("[PlanetTutorialTrace] missing GameRules reference", this);
                hasReferences = false;
            }

            if (_gameUI == null)
            {
                Debug.LogWarning("[PlanetTutorialTrace] missing GameUI reference", this);
                hasReferences = false;
            }

            return hasReferences;
        }

        private PlanetTutorialStage LoadStage()
        {
            int stageValue = PlayerPrefs.GetInt(CurrentStageKey, (int)PlanetTutorialStage.FirstVisit);

            if (stageValue < (int)PlanetTutorialStage.FirstVisit ||
                stageValue > (int)PlanetTutorialStage.BuySmallTreeManager)
            {
                Debug.LogWarning($"[PlanetTutorialTrace] saved stage out of range value={stageValue}, falling back to FirstVisit", this);
                return PlanetTutorialStage.FirstVisit;
            }

            return (PlanetTutorialStage)stageValue;
        }

        private void SaveStage(PlanetTutorialStage stage)
        {
            PlayerPrefs.SetInt(CurrentStageKey, (int)stage);
            PlayerPrefs.SetString(CurrentStepIdKey, stage.ToString());
            PlayerPrefs.Save();
        }

        private static bool IsCompleted()
        {
            return PlayerPrefs.GetInt(CompletedKey, 0) == 1;
        }

        private static void SaveFlag(string key)
        {
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }

        private double GetCurrentMoney()
        {
            return _gameRules != null ? _gameRules.CurrentMoney : 0d;
        }

        private static string GetObjectName(GameObject target)
        {
            return target != null ? target.name : "null";
        }
    }
}
