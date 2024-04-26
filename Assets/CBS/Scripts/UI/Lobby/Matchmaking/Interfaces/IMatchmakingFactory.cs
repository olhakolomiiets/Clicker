using CBS.Models;
using UnityEngine;

namespace CBS.UI
{
    public interface IMatchmakingFactory
    {
        GameObject SpawnMatchmakingResult(CBSMatchmakingQueue queue);
    }
}
