using CBS.Models;
using System;

namespace CBS
{
    public interface IClanExpirience
    {
        /// <summary>
        /// Adds N points of experience to the current state. In the response, you can get information whether the clan has reached a new level and also information about the reward about the new level.
        /// </summary>
        /// <param name="expToAdd"></param>
        /// <param name="result"></param>
        void AddExpirienceToClan(string clanID, int expToAdd, Action<CBSLevelDataResult> result = null);

        /// <summary>
        /// Get information about current experience/level of clan
        /// </summary>
        /// <param name="clanID"></param>
        /// <param name="result"></param>
        void GetClanLevelDetail(string clanID, Action<CBSLevelDataResult> result);
    }
}
