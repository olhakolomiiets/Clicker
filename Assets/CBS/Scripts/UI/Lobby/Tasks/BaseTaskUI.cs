using CBS.Core;
using CBS.Models;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class BaseTaskUI<T> : MonoBehaviour, IScrollableItem<T> where T : CBSTask
    {
        [SerializeField]
        private Text Title;
        [SerializeField]
        private Text Description;
        [SerializeField]
        private Text StepsLabel;
        [SerializeField]
        private Text NotifyLabel;
        [SerializeField]
        private Image IconImage;
        [SerializeField]
        private Slider StepsSlider;
        [SerializeField]
        private Text LevelTitle;

        [SerializeField]
        protected GameObject IconLock;
        [SerializeField]
        protected GameObject CompleteBtn;
        [SerializeField]
        protected GameObject LockBtn;
        [SerializeField]
        protected GameObject AddPointBt;
        [SerializeField]
        protected GameObject RewardBt;
        [SerializeField]
        protected GameObject LevelIcon;

        protected T Task { get; set; }

        protected virtual string LockText { get; }
        protected virtual string NotCompleteText { get; }
        protected virtual string CompleteText { get; }


        private void Awake()
        {
            OnInit();
        }

        protected virtual void OnInit() { }

        public void Display(T task)
        {
            Task = task;
            var isCompleted = Task.IsComplete;
            var rewardAvailable = Task.IsRewardAvailable();

            ToDefault();
            // draw title
            Title.text = Task.Title;
            Description.text = Task.Description;
            // draw sprite
            var iconSprite = Task.GetSprite();
            IconImage.gameObject.SetActive(iconSprite != null);
            IconImage.sprite = iconSprite;
            // check locked
            bool isLocked = !Task.IsAvailable;
            LockBtn.SetActive(isLocked);
            IconLock.SetActive(isLocked);
            if (isLocked)
                NotifyLabel.text = LockText;
            // draw slider state
            if (!isLocked && Task.Type == TaskType.ONE_SHOT)
            {
                NotifyLabel.text = NotCompleteText;
            }
            else if ((Task.Type == TaskType.STEPS || Task.Type == TaskType.TIERED) && !isLocked)
            {
                StepsSlider.gameObject.SetActive(true);
                StepsSlider.maxValue = Task.Steps;
                StepsSlider.value = Task.CurrentStep;
                var sliderTitle = string.Format("{0}/{1}", Task.CurrentStep, Task.Steps);
                StepsLabel.text = sliderTitle;
                StepsSlider.gameObject.SetActive(!isCompleted);
            }
            // draw buttons
            if (isCompleted)
                NotifyLabel.text = CompleteText;
            // check tiered
            if (Task.Type == TaskType.TIERED)
            {
                LevelTitle.text = Task.TierIndex.ToString();
            }

            DrawButtons(isLocked, isCompleted, rewardAvailable);
        }

        protected virtual void DrawButtons(bool isLocked, bool isCompleted, bool rewardAvailable)
        {
            AddPointBt.SetActive(!isLocked && !isCompleted);
            LockBtn.SetActive(isLocked && !isCompleted);
            RewardBt.SetActive((!isLocked && isCompleted && rewardAvailable) || (Task.Type == TaskType.TIERED && !isLocked && rewardAvailable));
            CompleteBtn.SetActive(!isLocked && isCompleted && !rewardAvailable);
            LevelIcon.SetActive(Task.Type == TaskType.TIERED);
        }

        // button click
        public virtual void OnAddPoint() { }

        public virtual void GetRewards() { }

        public void ShowRewards()
        {
            var reward = Task.Reward;
            new PopupViewer().ShowRewardPopup(reward);
        }

        private void ToDefault()
        {
            Title.text = string.Empty;
            Description.text = string.Empty;
            StepsLabel.text = string.Empty;
            NotifyLabel.text = string.Empty;

            IconImage.gameObject.SetActive(false);
            StepsSlider.gameObject.SetActive(false);
            IconLock.SetActive(false);
            CompleteBtn.SetActive(false);
            LockBtn.SetActive(false);
            AddPointBt.SetActive(false);
        }
    }
}
