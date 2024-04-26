
using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetBattlePassFullInformationResult : CBSBaseResult
    {
        public BattlePassUserInfo ProfileState;
        public BattlePassInstance Instance;

        public List<BattlePassLevelInfo> GetLevelTreeDetailList()
        {
            if (Instance == null || Instance.LevelTree == null)
                return new List<BattlePassLevelInfo>();
            var levelTree = Instance.GetLevelTree();
            var levelsInfo = new List<BattlePassLevelInfo>();
            var maxIndex = levelTree.Count - 1;
            for (int i = 0; i <= maxIndex; i++)
            {
                var level = levelTree[i];
                levelsInfo.Add(new BattlePassLevelInfo(Instance, ProfileState, level, i, maxIndex));
            }
            return levelsInfo;
        }

        public List<BattlePassBankLevel> GetBankLevelTree()
        {
            var bankAvailable = ProfileState.BankAccess;
            var passLevel = ProfileState.PlayerLevel;
            return Instance.GetBankLevels(passLevel, bankAvailable);
        }
    }
}
