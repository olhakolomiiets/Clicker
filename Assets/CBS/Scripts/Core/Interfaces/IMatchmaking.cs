using CBS.Models;
using System;

namespace CBS
{
    public interface IMatchmaking
    {
        /// <summary>
        /// Notifies about status change in Matchmaking
        /// </summary>
        event Action<MatchmakingStatus> OnStatusChanged;

        /// <summary>
        /// Notifies about the successful completion of the search for opponents.
        /// </summary>
        event Action<CBSStartMatchResult> OnMatchStart;

        /// <summary>
        /// Current Queue name
        /// </summary>
        string ActiveQueue { get; }

        /// <summary>
        /// Current ticket id name
        /// </summary>
        string ActiveTicketID { get; }

        /// <summary>
        /// Active matchmaking status
        /// </summary>
        MatchmakingStatus Status { get; }

        /// <summary>
        /// Get a list of all Queues on the server
        /// </summary>
        /// <param name="result"></param>
        void GetMatchmakingQueuesList(Action<CBSGetMatchmakingListResult> result);

        /// <summary>
        /// Creates a ticket to find opponents. After a successful call, listen for a change in the status of the queue.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void FindMatch(CBSFindMatchRequest request, Action<CBSFindMatchResult> result);

        /// <summary>
        /// Get a detailed description of the queue by name.
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="result"></param>
        void GetMatchmakingQueue(string queueName, Action<CBSGetQueueResult> result);

        /// <summary>
        /// Cancels all search tickets for the current player.
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="result"></param>
        void CancelMatch(string queueName, Action<CBSBaseResult> result);

        /// <summary>
        /// Get detail information about match
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="matchID"></param>
        /// <param name="result"></param>
        void GetMatch(string queueName, string matchID, Action<CBSGetMatchResult> result);
    }
}
