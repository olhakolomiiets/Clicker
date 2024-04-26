using PlayFab;
using PlayFab.CloudScriptModels;
using PlayFab.MultiplayerModels;
using System;
using System.Collections.Generic;
using EntityKey = PlayFab.MultiplayerModels.EntityKey;

namespace CBS.Playfab
{
    public interface IFabMatchmaking
    {
        void GetMatchmakingList(Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void CreateTicket(string queueName, int waitTime, string entityID, List<EntityKey> membersToMatchWith, MatchmakingPlayerAttributes atributes, Action<CreateMatchmakingTicketResult> onCreate, Action<PlayFabError> onFailed);

        void GetMatchmakingTicket(string queueName, string ticketID, Action<GetMatchmakingTicketResult> onGet, Action<PlayFabError> onFailed);

        void GetQueue(string queueName, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void GetMatch(string queueName, string matchID, Action<GetMatchResult> onGet, Action<PlayFabError> onFailed);

        void CancelMatchForPlayer(string queueName, string entityID, Action<CancelAllMatchmakingTicketsForPlayerResult> onCancel, Action<PlayFabError> onFailed);

        void GetMatch(string queueName, string matchID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
#if UNITY_EDITOR
        void UpdateMatchmakingQueue(MatchmakingQueueConfig queue, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);

        void RemoveMatchmakingQueue(string queueName, Action<ExecuteFunctionResult> onRemove, Action<PlayFabError> onFailed);
#endif
    }
}
