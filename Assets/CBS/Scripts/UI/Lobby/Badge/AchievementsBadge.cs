using CBS.Models;

namespace CBS.UI
{
    public class AchievementsBadge : BaseBadge
    {
        private IAchievements CBSAchievements { get; set; }

        private void Awake()
        {
            CBSAchievements = CBSModule.Get<CBSAchievementsModule>();

            CBSAchievements.OnCompleteAchievement += OnCompleteAchievement;
            CBSAchievements.OnProfileRewarded += OnPlayerRewarded;
            CBSAchievements.OnCompleteAchievementTier += OnCompleteTier;

            UpdateCount(0);
        }

        private void OnDestroy()
        {
            CBSAchievements.OnCompleteAchievement -= OnCompleteAchievement;
            CBSAchievements.OnProfileRewarded -= OnPlayerRewarded;
            CBSAchievements.OnCompleteAchievementTier -= OnCompleteTier;
        }

        private void OnEnable()
        {
            GetAchievementsBadge();
        }

        private void GetAchievementsBadge()
        {
            CBSAchievements.GetAchievementsBadge(OnGetBadge);
        }

        private void OnGetBadge(CBSBadgeResult result)
        {
            if (result.IsSuccess)
            {
                var notRewardedAchievements = result.Count;
                UpdateCount(notRewardedAchievements);
            }
        }

        private void OnPlayerRewarded(GrantRewardResult reward)
        {
            GetAchievementsBadge();
        }

        private void OnCompleteAchievement(CBSTask task)
        {
            GetAchievementsBadge();
        }

        private void OnCompleteTier(CBSTask task)
        {
            GetAchievementsBadge();
        }
    }
}
