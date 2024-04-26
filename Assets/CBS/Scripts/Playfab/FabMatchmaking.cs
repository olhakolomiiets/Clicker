using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using PlayFab.MultiplayerModels;
using System;
using System.Collections.Generic;
using EntityKey = PlayFab.MultiplayerModels.EntityKey;

namespace CBS.Playfab
{
    public class FabMatchmaking : FabExecuter, IFabMatchmaking
    {
        public void GetMatchmakingList(Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetMatchmakingListMethod,
                FunctionParameter = new FunctionMatchmakingQueueRequest()
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void CreateTicket(string queueName, int waitTime, string entityID, List<EntityKey> membersToMatchWith, MatchmakingPlayerAttributes attributes, Action<CreateMatchmakingTicketResult> onCreate, Action<PlayFabError> onFailed)
        {
            var request = new CreateMatchmakingTicketRequest
            {
                Creator = new MatchmakingPlayer
                {
                    Attributes = attributes,
                    Entity = new PlayFab.MultiplayerModels.EntityKey
                    {
                        Id = entityID,
                        Type = CBSConstants.EntityPlayerType
                    }
                },
                QueueName = queueName,
                GiveUpAfterSeconds = waitTime,
                MembersToMatchWith = membersToMatchWith
            };
            PlayFabMultiplayerAPI.CreateMatchmakingTicket(request, onCreate, onFailed);
        }

        public void GetMatchmakingTicket(string queueName, string ticketID, Action<GetMatchmakingTicketResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new GetMatchmakingTicketRequest
            {
                QueueName = queueName,
                TicketId = ticketID
            };
            PlayFabMultiplayerAPI.GetMatchmakingTicket(request, onGet, onFailed);
        }

        public void GetQueue(string queueName, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetMatchmakingQueueMethod,
                FunctionParameter = new FunctionMatchmakingQueueRequest
                {
                    Queue = queueName
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void GetMatch(string queueName, string matchID, Action<GetMatchResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new GetMatchRequest
            {
                QueueName = queueName,
                MatchId = matchID,
                ReturnMemberAttributes = true
            };
            PlayFabMultiplayerAPI.GetMatch(request, onGet, onFailed);
        }

        public void CancelMatchForPlayer(string queueName, string entityID, Action<CancelAllMatchmakingTicketsForPlayerResult> onCancel, Action<PlayFabError> onFailed)
        {
            var request = new CancelAllMatchmakingTicketsForPlayerRequest
            {
                QueueName = queueName,
                Entity = new PlayFab.MultiplayerModels.EntityKey
                {
                    Id = entityID,
                    Type = CBSConstants.EntityPlayerType
                }
            };
            PlayFabMultiplayerAPI.CancelAllMatchmakingTicketsForPlayer(request, onCancel, onFailed);
        }

        public void GetMatch(string queueName, string matchID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetMatchMethod,
                FunctionParameter = new FunctionMatchRequest
                {
                    Queue = queueName,
                    MatchID = matchID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

#if UNITY_EDITOR
        public void UpdateMatchmakingQueue(MatchmakingQueueConfig queue, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UpdateMatchmakingQueueMethod,
                FunctionParameter = new FunctionMatchmakingQueueRequest
                {
                    Queue = queue.ToJson()
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpdate, onFailed);
        }

        public void RemoveMatchmakingQueue(string queueName, Action<ExecuteFunctionResult> onRemove, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.RemoveMatchmakingQueueMethod,
                FunctionParameter = new FunctionMatchmakingQueueRequest
                {
                    Queue = queueName
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onRemove, onFailed);
        }
#endif
    }
}
