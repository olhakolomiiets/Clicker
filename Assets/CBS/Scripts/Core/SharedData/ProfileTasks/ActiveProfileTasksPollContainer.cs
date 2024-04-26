using System.Collections.Generic;

namespace CBS.Models
{
    public class ActiveProfileTasksPoolContainer
    {
        public Dictionary<string, List<string>> PoolStates;

        public List<string> GetSavedForPool(string poolID)
        {
            if (PoolStates == null)
                return new List<string>();
            if (!PoolStates.ContainsKey(poolID))
                return new List<string>();
            return PoolStates[poolID] ?? new List<string>();
        }

        public void AddState(string poolID, List<string> tasksIDs)
        {
            if (PoolStates == null)
                PoolStates = new Dictionary<string, List<string>>();
            PoolStates[poolID] = tasksIDs;
        }
    }
}


