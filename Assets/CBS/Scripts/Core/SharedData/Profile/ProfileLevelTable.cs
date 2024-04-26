

using System.Collections.Generic;

namespace CBS.Models
{
    public class ProfileLevelTable : LevelTable
    {
        public ProfileEventContainer RegistrationEvents;
        public RewardDelivery RewardDelivery;
        public List<ProfileEventContainer> LevelEvents;

        public override void AddLevelDetail(LevelDetail level)
        {
            base.AddLevelDetail(level);
            if (LevelEvents == null)
                LevelEvents = new List<ProfileEventContainer>();
            LevelEvents.Add(new ProfileEventContainer());
        }

        public override void RemoveLevelDetailAt(int index)
        {
            base.RemoveLevelDetailAt(index);
            if (LevelEvents == null)
                return;
            if (index < LevelEvents.Count)
            {
                LevelEvents.RemoveAt(index);
                LevelEvents.TrimExcess();
            }
        }

    }
}

