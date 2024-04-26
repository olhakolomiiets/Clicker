using CBS.Models;
using CBS.Playfab;
using CBS.Utils;
using PlayFab;
using PlayFab.MultiplayerModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace CBS
{
    public class CBSMatchmakingModule : CBSModule, IMatchmaking
    {
        /// <summary>
        /// Notifies about status change in Matchmaking
        /// </summary>
        public event Action<MatchmakingStatus> OnStatusChanged;
        /// <summary>
        /// Notifies about the successful completion of the search for opponents.
        /// </summary>
        public event Action<CBSStartMatchResult> OnMatchStart;

        private IFabMatchmaking FabMatchmaking { get; set; }
        private IProfile Profile { get; set; }

        /// <summary>
        /// Current Queue name
        /// </summary>
        public string ActiveQueue { get; private set; }
        /// <summary>
        /// Current ticket id name
        /// </summary>
        public string ActiveTicketID { get; private set; }
        /// <summary>
        /// Active matchmaking status
        /// </summary>
        public MatchmakingStatus Status { get; private set; }

        private bool CompareIsRunning { get; set; }
        private Coroutine CompareCoroutine { get; set; }

        protected override void Init()
        {
            FabMatchmaking = FabExecuter.Get<FabMatchmaking>();
            Profile = Get<CBSProfileModule>();
        }

        // API Methods

        /// <summary>
        /// Creates a ticket to find opponents. After a successful call, listen for a change in the status of the queue.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void FindMatch(CBSFindMatchRequest request, Action<CBSFindMatchResult> result)
        {
            var entityId = Profile.EntityID;
            var profileID = Profile.ProfileID;
            var queueName = request.QueueName;
            var waitTime = request.WaitTime ?? CBSConstants.MatchmakingDefaultWaitTime;
            var membersToMatchWith = request.MembersToMatchWith;

            var jsonPlugin = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
            var playerLevel = Profile.CachedLevelInfo.CurrentLevel;

            // clear prev match is exist
            CancelMatch(queueName, onCancel =>
            {
                if (onCancel.IsSuccess)
                {
                    // get queue
                    GetMatchmakingQueue(queueName, onGetQueue =>
                    {
                        if (onGetQueue.IsSuccess)
                        {
                            var queue = onGetQueue.Queue;
                            int? nullableLevel = null;
                            double? nullableValue = null;

                            var dataObject = new CBSPlayerAttributes
                            {
                                ProfileID = profileID,
                                LevelEquality = queue.IsLevelEquality ? playerLevel.ToString() : string.Empty,
                                MatchmakingStringEquality = queue.IsStringEquality ? request.StringEqualityValue : string.Empty,
                                LevelDifference = queue.IsLevelDifference ? playerLevel : nullableLevel,
                                ValueDifference = queue.IsValueDifference ? request.DifferenceValue : nullableValue
                            };
                            var rawAttributes = jsonPlugin.SerializeObject(dataObject);

                            var playerAttributes = new MatchmakingPlayerAttributes
                            {
                                EscapedDataObject = rawAttributes
                            };

                            // create ticket
                            FabMatchmaking.CreateTicket(queueName, waitTime, entityId, membersToMatchWith, playerAttributes, onCreate =>
                            {
                                var ticketID = onCreate.TicketId;
                                ActiveQueue = queueName;
                                ActiveTicketID = ticketID;

                                FabMatchmaking.GetMatchmakingTicket(queueName, ticketID, onGetTicket =>
                                {
                                    var date = onGetTicket.Created;
                                    var members = onGetTicket.Members;

                                    result?.Invoke(new CBSFindMatchResult
                                    {
                                        IsSuccess = true,
                                        TicketID = ticketID,
                                        Queue = queue,
                                        CreatedDate = date
                                    });

                                    SetStatus(MatchmakingStatus.CreateTicket);

                                    StartRefreshTask();

                                }, OnFailedGetTicket =>
                                {
                                    // failed get ticket
                                    result?.Invoke(new CBSFindMatchResult
                                    {
                                        IsSuccess = false,
                                        Error = CBSError.FromTemplate(OnFailedGetTicket)
                                    });
                                });
                            }, onFailed =>
                            {
                                // failed create ticket
                                result?.Invoke(new CBSFindMatchResult
                                {
                                    IsSuccess = false,
                                    Error = CBSError.FromTemplate(onFailed)
                                });
                            });
                        }
                        else
                        {
                            // failed get queue
                            result?.Invoke(new CBSFindMatchResult
                            {
                                IsSuccess = false,
                                Error = onGetQueue.Error
                            });
                        }
                    });
                }
                else
                {
                    // failed clear tickets
                    result?.Invoke(new CBSFindMatchResult
                    {
                        IsSuccess = false,
                        Error = onCancel.Error
                    });
                }
            });
        }

        /// <summary>
        /// Get a detailed description of the queue by name.
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="result"></param>
        public void GetMatchmakingQueue(string queueName, Action<CBSGetQueueResult> result)
        {
            FabMatchmaking.GetQueue(queueName, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetQueueResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var fabResult = onGet.GetResult<GetMatchmakingQueueResult>();
                    var fabQueue = fabResult.MatchmakingQueue;
                    var cbsQueue = CBSMatchmakingQueue.FromMatchConfig(fabQueue);

                    result?.Invoke(new CBSGetQueueResult
                    {
                        IsSuccess = true,
                        Queue = cbsQueue
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetQueueResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get a list of all Queues on the server.
        /// </summary>
        /// <param name="result"></param>
        public void GetMatchmakingQueuesList(Action<CBSGetMatchmakingListResult> result)
        {
            FabMatchmaking.GetMatchmakingList(onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetMatchmakingListResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var fabResult = onGet.GetResult<ListMatchmakingQueuesResult>();
                    var fabQueuesResult = fabResult.MatchMakingQueues;
                    var cbsQueues = new List<CBSMatchmakingQueue>();

                    foreach (var fabQueue in fabQueuesResult)
                    {
                        var queueMode = fabQueue.Teams == null || fabQueue.Teams.Count == 0 ? MatchmakingMode.Single : MatchmakingMode.Team;
                        var newQueue = CBSMatchmakingQueue.FromMatchConfig(fabQueue);
                        cbsQueues.Add(newQueue);
                    }

                    result?.Invoke(new CBSGetMatchmakingListResult
                    {
                        IsSuccess = true,
                        Queues = cbsQueues
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetMatchmakingListResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Cancels all search tickets for the current player.
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="result"></param>
        public void CancelMatch(string queueName, Action<CBSBaseResult> result)
        {
            var entityID = Profile.EntityID;
            FabMatchmaking.CancelMatchForPlayer(queueName, entityID, onCancel =>
            {
                result?.Invoke(new CBSBaseResult
                {
                    IsSuccess = true
                });
                if (Status != MatchmakingStatus.None)
                {
                    SetStatus(MatchmakingStatus.Canceled);
                }
                ClearInternalProcess();
            }, onFailed =>
            {
                result?.Invoke(new CBSBaseResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get detail information about match
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="matchID"></param>
        /// <param name="result"></param>
        public void GetMatch(string queueName, string matchID, Action<CBSGetMatchResult> result)
        {
            FabMatchmaking.GetMatch(queueName, matchID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetMatchResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var fabResult = onGet.GetResult<GetMatchResult>();

                    result?.Invoke(new CBSGetMatchResult
                    {
                        IsSuccess = true,
                        ArrangementString = fabResult.ArrangementString,
                        MatchId = fabResult.MatchId,
                        Members = fabResult.Members.Select(x => CBSMatchmakingPlayer.FromFabModel(x)).ToList(),
                        RegionPreferences = fabResult.RegionPreferences,
                        ServerDetails = fabResult.ServerDetails
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetMatchResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        // internal
        private void StartRefreshTask()
        {
            CompareIsRunning = true;
            CompareWorkTask();
        }

        private void StopRefreshTask()
        {
            CompareIsRunning = false;
            if (CompareCoroutine != null)
            {
                CoroutineRunner.StopCoroutine(CompareCoroutine);
            }
        }

        private void CompareWorkTask()
        {
            CompareCoroutine = CoroutineRunner.StartCoroutine(CompareWorkTaskCoroutine());
        }

        private IEnumerator CompareWorkTaskCoroutine()
        {
            yield return new WaitForSeconds(CBSConstants.MatchmakingRefreshTime);

            if (!CompareIsRunning)
            {
                yield break;
            }

            FabMatchmaking.GetMatchmakingTicket(ActiveQueue, ActiveTicketID, onGet =>
            {
                var status = onGet.Status;
                var members = onGet.Members;
                switch (status)
                {
                    case MatchmakingUtils.StatusCanceled:
                        SetStatus(MatchmakingStatus.Canceled);
                        ClearInternalProcess();
                        break;
                    case MatchmakingUtils.StatusMatched:
                        StartMatch(onGet);
                        StopRefreshTask();
                        break;
                    case MatchmakingUtils.StatusWaitingForMatch:
                        SetStatus(MatchmakingStatus.WaitingForMatch);
                        break;
                    case MatchmakingUtils.StatusWaitingForPlayers:
                        SetStatus(MatchmakingStatus.WaitingForPlayers);
                        break;
                }

                CompareWorkTask();
            }, onError =>
            {
                CompareWorkTask();
            });
        }

        private void SetStatus(MatchmakingStatus status)
        {
            if (Status != status)
            {
                OnStatusChanged?.Invoke(status);
            }
            Status = status;
        }

        private void StartMatch(GetMatchmakingTicketResult result)
        {
            var matchID = result.MatchId;
            var queueName = result.QueueName;

            FabMatchmaking.GetMatch(queueName, matchID, onGet =>
            {
                var members = onGet.Members;
                var players = members.Select(x => CBSMatchmakingPlayer.FromFabModel(x)).ToList();
                SetStatus(MatchmakingStatus.Matched);

                var matchResult = new CBSStartMatchResult
                {
                    MatchID = onGet.MatchId,
                    TicketID = ActiveTicketID,
                    QueueName = queueName,
                    Players = players
                };

                OnMatchStart?.Invoke(matchResult);
            }, onFailed =>
            {
                CancelMatch(queueName, onCancel =>
                {
                    ClearInternalProcess();
                });
            });
        }

        protected override void OnLogout()
        {
            base.OnLogout();
            ClearInternalProcess();
        }

        private void ClearInternalProcess()
        {
            ActiveQueue = string.Empty;
            ActiveTicketID = string.Empty;

            StopRefreshTask();

            SetStatus(MatchmakingStatus.None);
        }
    }
}
