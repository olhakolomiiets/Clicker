using System;
using System.Collections.Generic;

namespace CBS.Models
{
    [Serializable]
    public class LevelTable
    {
        private static readonly float DefaultExpMultiply = 1f;

        public float? ExpMultiply;
        public RewardObject RegistrationPrize;
        public List<LevelDetail> Table = new List<LevelDetail>();

        public float GetExpMultiply()
        {
            if (ExpMultiply == null)
                return DefaultExpMultiply;
            return ExpMultiply.GetValueOrDefault();
        }

        public virtual void AddLevelDetail(LevelDetail level)
        {
            if (Table == null)
                Table = new List<LevelDetail>();
            Table.Add(level);
        }

        public virtual void RemoveLevelDetailAt(int index)
        {
            if (Table == null)
                return;
            if (index < Table.Count)
            {
                Table.RemoveAt(index);
                Table.TrimExcess();
            }
        }
    }
}
