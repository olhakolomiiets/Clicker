namespace CBS.Models
{
    [System.Serializable]
    public class TaskTier
    {
        public int Index;
        public int StepsToComplete;
        public int CurrentSteps;
        public bool OverrideDescription;
        public string Description;

        public RewardObject Reward;
        public ProfileEventContainer Events;
        public RewardObject AdditionalReward;
        public ClanEventContainer ClanEvents;

        public bool AddPoints(int points, int currentSteps)
        {
            currentSteps += points;
            if (currentSteps >= StepsToComplete)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
