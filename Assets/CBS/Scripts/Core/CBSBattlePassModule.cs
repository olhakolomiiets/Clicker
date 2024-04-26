using CBS.Models;
using CBS.Playfab;
using CBS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CBS
{
    public class CBSBattlePassModule : CBSModule, IBattlePass
    {
        private IFabBattlePass FabBattlePass { get; set; }
        private IProfile Profile { get; set; }
        private ICBSInAppPurchase InAppPurchase { get; set; }

        /// <summary>
        /// Notify when experience has been gained for a specific Battle Pass.
        /// </summary>
        public event Action<string, int> OnExpirienceAdded;
        /// <summary>
        /// Notify when the reward was received.
        /// </summary>
        public event Action<GrantRewardResult> OnRewardRecived;
        /// <summary>
        /// Notify when profile bought a ticket.
        /// </summary>
        public event Action<BattlePassTicket> OnTicketPurchased;

        protected override void Init()
        {
            FabBattlePass = FabExecuter.Get<FabBattlePass>();
            Profile = Get<CBSProfileModule>();
            InAppPurchase = Get<CBSInAppPurchaseModule>();
        }

        // Method API

        /// <summary>
        /// Get information about the state of the player's instances of Battle passes. Does not contain complete information about levels and rewards. More suitable for implementing badges.
        /// </summary>
        /// <param name="result"></param>
        public void GetBattlePassStates(Action<CBSGetPlayerBattlePassStatesResult> result)
        {
            GetProfileStatesFromRequest(new CBSBattlePassProfileStateRequest
            {
                SpecificID = string.Empty,
            }, result);
        }

        /// <summary>
        /// Get information about the state of Battle Pass instance. Does not contain complete information about levels and rewards. More suitable for implementing badges.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void GetBattlePassState(string battlePassID, Action<CBSGetPlayerBattlePassStatesResult> result)
        {
            GetProfileStatesFromRequest(new CBSBattlePassProfileStateRequest
            {
                SpecificID = battlePassID,
            }, result);
        }

        /// <summary>
        /// Get complete information about the state of the player's instances of Battle passes and instance levels. Contains complete information about levels and rewards.
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="result"></param>
        public void GetBattlePassFullInformation(string battlePassID, Action<CBSGetBattlePassFullInformationResult> result)
        {
            var profileID = Profile.ProfileID;
            FabBattlePass.GetBattlePassFullInformation(profileID, battlePassID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetBattlePassFullInformationResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionBattlePassFullInfoResult>();
                    result?.Invoke(new CBSGetBattlePassFullInformationResult
                    {
                        IsSuccess = true,
                        ProfileState = functionResult.ProfileState,
                        Instance = functionResult.Instance
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetBattlePassFullInformationResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Add player experience for a specific instance of Battle Pass.
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="exp"></param>
        /// <param name="result"></param>
        public void AddExpirienceToInstance(string battlePassID, int exp, Action<CBSAddExpirienceToInstanceResult> result)
        {
            var profileID = Profile.ProfileID;
            FabBattlePass.AddExpirienceToInstance(profileID, exp, battlePassID, onAdd =>
            {
                var cbsError = onAdd.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSAddExpirienceToInstanceResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onAdd.GetResult<FunctionAddPassAddExpResult>();
                    var resulteTable = functionResult.ExpTable;
                    resulteTable = resulteTable ?? new Dictionary<string, int>();
                    var defaultResult = resulteTable.FirstOrDefault();
                    OnExpirienceAdded?.Invoke(defaultResult.Key, defaultResult.Value);

                    result?.Invoke(new CBSAddExpirienceToInstanceResult
                    {
                        IsSuccess = true,
                        BattlePassID = resulteTable == null ? string.Empty : defaultResult.Key,
                        NewExp = resulteTable == null ? 0 : defaultResult.Value
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSAddExpirienceToInstanceResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Add player experience for all active instances of Battle Passes.
        /// </summary>
        /// <param name="exp"></param>
        /// <param name="result"></param>
        public void AddExpirienceToAllActiveInstances(int exp, Action<CBSAddExpirienceToAllInstancesResult> result)
        {
            var profileID = Profile.ProfileID;
            FabBattlePass.AddExpirienceToInstance(profileID, exp, string.Empty, onAdd =>
            {
                var cbsError = onAdd.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSAddExpirienceToAllInstancesResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onAdd.GetResult<FunctionAddPassAddExpResult>();
                    var resulteTable = functionResult.ExpTable;

                    foreach (var res in resulteTable)
                    {
                        OnExpirienceAdded?.Invoke(res.Key, res.Value);
                    }

                    result?.Invoke(new CBSAddExpirienceToAllInstancesResult
                    {
                        IsSuccess = true,
                        NewExpTable = resulteTable
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSAddExpirienceToAllInstancesResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Grant the player a reward from a specific instance of Battle Pass.
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="level"></param>
        /// <param name="isPremium"></param>
        /// <param name="result"></param>
        public void GrantAwardToProfile(string battlePassID, int level, bool isPremium, Action<CBSGrantAwardToPlayerResult> result)
        {
            var profileID = Profile.ProfileID;
            FabBattlePass.GetRewardFromInstance(profileID, battlePassID, level, isPremium, onGrant =>
            {
                var cbsError = onGrant.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGrantAwardToPlayerResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGrant.GetResult<FunctionBattlePassGrantRewardResult>();
                    var premium = functionResult.IsPremium;
                    var passID = functionResult.InstanceID;
                    var grantResult = functionResult.RewardResult;

                    if (functionResult != null && grantResult != null)
                    {
                        var currencies = grantResult.GrantedCurrencies;
                        if (currencies != null)
                        {
                            var codes = currencies.Select(x => x.Key).ToArray();
                            Get<CBSCurrencyModule>().ChangeRequest(codes);
                        }
                        OnRewardRecived?.Invoke(grantResult);

                        var grantedInstances = grantResult.GrantedInstances;
                        if (grantedInstances != null && grantedInstances.Count > 0)
                        {
                            var inventoryItems = grantedInstances.Select(x => x.ToCBSInventoryItem()).ToList();
                            Get<CBSInventoryModule>().AddRequest(inventoryItems);
                        }
                    }

                    result?.Invoke(new CBSGrantAwardToPlayerResult
                    {
                        IsSuccess = true,
                        BattlePassID = passID,
                        IsPremium = premium,
                        RecivedReward = grantResult
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGrantAwardToPlayerResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Grant ticket to profile for active battle pass instance
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="ticketID"></param>
        /// <param name="result"></param>
        public void GrantTicket(string battlePassID, string ticketID, Action<CBSGrantTicketResult> result)
        {
            var profileID = Profile.ProfileID;
            FabBattlePass.GrantTicket(profileID, battlePassID, ticketID, onGrant =>
            {
                var cbsError = onGrant.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGrantTicketResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGrant.GetResult<FunctionGrantTicketResult>();
                    var passID = functionResult.BattlePassID;
                    var passInstanceID = functionResult.BattlePassInstanceID;
                    var purchasedTicketID = functionResult.TicketID;
                    var ticket = functionResult.Ticket;
                    var ticketCatalogID = functionResult.TicketCatalogID;

                    OnTicketPurchased?.Invoke(ticket);

                    result?.Invoke(new CBSGrantTicketResult
                    {
                        IsSuccess = true,
                        BattlePassID = passID,
                        BattlePassInstanceID = passInstanceID,
                        TicketID = purchasedTicketID,
                        Ticket = ticket,
                        TicketCatalogID = ticketCatalogID
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGrantTicketResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Reset player data for a specific Battle Pass.
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="result"></param>
        public void ResetBattlePassStateForProfile(string battlePassID, Action<CBSResetBattlePassStateResult> result)
        {
            var profileID = Profile.ProfileID;
            FabBattlePass.ResetInstanceForProfile(profileID, battlePassID, onReset =>
            {
                var cbsError = onReset.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSResetBattlePassStateResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    result?.Invoke(new CBSResetBattlePassStateResult
                    {
                        IsSuccess = true,
                        BattlePassID = battlePassID
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSResetBattlePassStateResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Purchase ticket for active battle pass instance
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="ticketID"></param>
        /// <param name="result"></param>
        public void PurchaseTicket(string battlePassID, string ticketID, Action<CBSPurchaseTicketResult> result)
        {
            var profileID = Profile.ProfileID;
            FabBattlePass.PrePurchaseValidation(profileID, battlePassID, ticketID, onPreResult =>
            {
                var cbsError = onPreResult.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSPurchaseTicketResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onPreResult.GetResult<BattlePassInstance>();
                    var tickets = functionResult.GetPaidTickets();
                    var ticket = tickets.FirstOrDefault(x => x.ID == ticketID);
                    var catalogTicketID = ticket.GetCatalogID();
                    var cbsPrice = ticket.Price;

                    FabBattlePass.PurchaseTicket(catalogTicketID, cbsPrice.CurrencyID, cbsPrice.CurrencyValue, onPurchase =>
                    {
                        FabBattlePass.PostPurchaseValidation(profileID, battlePassID, ticketID, onPostResult =>
                        {
                            var postResult = onPostResult.GetResult<FunctionPostPurchaseTicketResult>();
                            var passID = postResult.BattlePassID;
                            var passInstanceID = postResult.BattlePassInstanceID;
                            var purchasedTicketID = postResult.TicketID;

                            Get<CBSCurrencyModule>().ChangeRequest(cbsPrice.CurrencyID);

                            OnTicketPurchased?.Invoke(ticket);

                            result?.Invoke(new CBSPurchaseTicketResult
                            {
                                IsSuccess = true,
                                BattlePassID = passID,
                                BattlePassInstanceID = passInstanceID,
                                TicketID = purchasedTicketID,
                                Ticket = ticket,
                                TicketCatalogID = catalogTicketID,
                                PriceCode = cbsPrice.CurrencyID,
                                PriceValue = cbsPrice.CurrencyValue
                            });
                        }, onFailed =>
                        {
                            result?.Invoke(new CBSPurchaseTicketResult
                            {
                                IsSuccess = false,
                                Error = CBSError.FromTemplate(onFailed)
                            });
                        });
                    }, onFailed =>
                    {
                        result?.Invoke(new CBSPurchaseTicketResult
                        {
                            IsSuccess = false,
                            Error = CBSError.FromTemplate(onFailed)
                        });
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSPurchaseTicketResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Purchase ticket with real money for active battle pass instance
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="ticketID"></param>
        /// <param name="result"></param>
        public void PurchaseTicketWithRealMoney(string battlePassID, string ticketID, Action<CBSPurchaseTicketWithRMResult> result)
        {
            var profileID = Profile.ProfileID;
            FabBattlePass.PrePurchaseValidation(profileID, battlePassID, ticketID, onPreResult =>
            {
                var cbsError = onPreResult.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSPurchaseTicketWithRMResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onPreResult.GetResult<BattlePassInstance>();
                    var tickets = functionResult.GetPaidTickets();
                    var ticket = tickets.FirstOrDefault(x => x.ID == ticketID);
                    var catalogTicketID = ticket.GetCatalogID();
                    var cbsPrice = ticket.Price;

                    InAppPurchase.PurchaseItem(catalogTicketID, CatalogKeys.BattlePassCatalogID, onPurchase =>
                    {
                        if (onPurchase.IsSuccess)
                        {
                            var transactionID = onPurchase.TransactionID;

                            FabBattlePass.PostPurchaseValidation(profileID, battlePassID, ticketID, onPostResult =>
                            {
                                var postResult = onPostResult.GetResult<FunctionPostPurchaseTicketResult>();
                                var passID = postResult.BattlePassID;
                                var passInstanceID = postResult.BattlePassInstanceID;
                                var purchasedTicketID = postResult.TicketID;

                                OnTicketPurchased?.Invoke(ticket);

                                result?.Invoke(new CBSPurchaseTicketWithRMResult
                                {
                                    IsSuccess = true,
                                    BattlePassID = passID,
                                    BattlePassInstanceID = passInstanceID,
                                    TicketID = purchasedTicketID,
                                    Ticket = ticket,
                                    TicketCatalogID = catalogTicketID,
                                    PriceCode = cbsPrice.CurrencyID,
                                    PriceValue = cbsPrice.CurrencyValue,
                                    TransactionID = transactionID
                                });
                            }, onFailed =>
                            {
                                result?.Invoke(new CBSPurchaseTicketWithRMResult
                                {
                                    IsSuccess = false,
                                    Error = CBSError.FromTemplate(onFailed)
                                });
                            });
                        }
                        else
                        {
                            result?.Invoke(new CBSPurchaseTicketWithRMResult
                            {
                                IsSuccess = false,
                                Error = onPurchase.Error
                            });
                        }
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSPurchaseTicketWithRMResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get tasks available for profile from pool of battle pass
        /// </summary>
        /// <param name="result"></param>
        public void GetBattlePassTasksForProfile(string battlePassID, Action<CBSGetTasksForProfileResult> result)
        {
            var profileID = Profile.ProfileID;

            FabBattlePass.GetBattlePassTasks(profileID, battlePassID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetTasksForProfileResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionProfileTasksResult>();
                    var tasks = functionResult.Tasks;
                    var resetDate = functionResult.NextResetDate;

                    result?.Invoke(new CBSGetTasksForProfileResult
                    {
                        IsSuccess = true,
                        Tasks = tasks,
                        ResetDate = resetDate
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetTasksForProfileResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Adds the points you specified to the task if task exist for battle pass
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="tasksPoolID"></param>
        /// <param name="taskID"></param>
        /// <param name="points"></param>
        /// <param name="result"></param>
        public void AddBattlePassTaskPoints(string battlePassID, string taskID, int points, Action<CBSModifyProfileTaskPointsResult> result)
        {
            var profileID = Profile.ProfileID;

            FabBattlePass.AddBattlePassTaskPoints(profileID, battlePassID, taskID, points, onAdd =>
            {
                var cbsError = onAdd.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSModifyProfileTaskPointsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onAdd.GetResult<FunctionModifyTaskResult<CBSProfileTask>>();
                    var achievement = functionResult.Task;
                    var reward = functionResult.RewardResult;
                    var complete = functionResult.CompleteTask;
                    var completeTier = functionResult.CompleteTier;

                    if (functionResult != null && reward != null)
                    {
                        var currencies = reward.GrantedCurrencies;
                        if (currencies != null)
                        {
                            var codes = currencies.Select(x => x.Key).ToArray();
                            Get<CBSCurrencyModule>().ChangeRequest(codes);
                        }

                        var grantedInstances = reward.GrantedInstances;
                        if (grantedInstances != null && grantedInstances.Count > 0)
                        {
                            var inventoryItems = grantedInstances.Select(x => x.ToCBSInventoryItem()).ToList();
                            Get<CBSInventoryModule>().AddRequest(inventoryItems);
                        }
                    }

                    result?.Invoke(new CBSModifyProfileTaskPointsResult
                    {
                        IsSuccess = true,
                        Task = achievement,
                        ReceivedReward = reward,
                        CompleteTask = complete,
                        CompleteTier = completeTier
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSModifyProfileTaskPointsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Pick up a reward from a completed task if it hasn't been picked up before.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="result"></param>
        public void PickupBattlePassTaskReward(string battlepassID, string taskID, Action<CBSModifyProfileTaskPointsResult> result)
        {
            var profileID = Profile.ProfileID;

            FabBattlePass.PickupBattlePassTaskReward(profileID, battlepassID, taskID, onPick =>
            {
                var cbsError = onPick.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSModifyProfileTaskPointsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onPick.GetResult<FunctionModifyTaskResult<CBSProfileTask>>();
                    var tasks = functionResult.Task;
                    var reward = functionResult.RewardResult;

                    if (functionResult != null && reward != null)
                    {
                        var currencies = reward.GrantedCurrencies;
                        if (currencies != null)
                        {
                            var codes = currencies.Select(x => x.Key).ToArray();
                            Get<CBSCurrencyModule>().ChangeRequest(codes);
                        }

                        var grantedInstances = reward.GrantedInstances;
                        if (grantedInstances != null && grantedInstances.Count > 0)
                        {
                            var inventoryItems = grantedInstances.Select(x => x.ToCBSInventoryItem()).ToList();
                            Get<CBSInventoryModule>().AddRequest(inventoryItems);
                        }
                    }

                    result?.Invoke(new CBSModifyProfileTaskPointsResult
                    {
                        IsSuccess = true,
                        Task = tasks,
                        ReceivedReward = reward
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSModifyProfileTaskPointsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        // internal
        private void GetProfileStatesFromRequest(CBSBattlePassProfileStateRequest request, Action<CBSGetPlayerBattlePassStatesResult> result)
        {
            var profileID = Profile.ProfileID;
            var specificID = request.SpecificID;

            FabBattlePass.GetProfileBattlePassStates(profileID, specificID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetPlayerBattlePassStatesResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionBattlePassStatesResult>();
                    var states = functionResult.ProfileStates;
                    result?.Invoke(new CBSGetPlayerBattlePassStatesResult
                    {
                        IsSuccess = true,
                        States = states
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetPlayerBattlePassStatesResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }
    }
}
