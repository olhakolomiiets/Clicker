using System.Collections.Generic;

namespace CBS.Models
{
    public class ClanLevelTable : LevelTable
    {
        public ClanEventContainer RegistrationEvents;
        public RewardDelivery RewardDelivery;
        public List<ClanEventContainer> LevelEvents;

        public override void AddLevelDetail(LevelDetail level)
        {
            base.AddLevelDetail(level);
            if (LevelEvents == null)
                LevelEvents = new List<ClanEventContainer>();
            LevelEvents.Add(new ClanEventContainer());
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

