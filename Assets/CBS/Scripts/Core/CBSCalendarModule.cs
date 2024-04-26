using CBS.Models;
using CBS.Playfab;
using CBS.Utils;
using System;
using System.Linq;

namespace CBS
{
    public class CBSCalendarModule : CBSModule, ICalendar
    {
        /// <summary>
        /// Notifies when a user has received a reward
        /// </summary>
        public event Action<GrantRewardResult> OnRewardCollected;

        /// <summary>
        /// Notifies when calelendar was reseted
        /// </summary>
        public event Action<CalendarInstance> OnCalendarReseted;

        /// <summary>
        /// Notifies when calelendar was granted
        /// </summary>
        public event Action<CalendarInstance> OnCalendarGranted;

        /// <summary>
        /// Notifies when calelendar was purchased
        /// </summary>
        public event Action<CalendarInstance> OnCalendarPurchased;

        private IFabCalendar FabCalendar { get; set; }
        private IProfile Profile { get; set; }
        private ICBSInAppPurchase InAppPurchase { get; set; }

        protected override void Init()
        {
            Profile = Get<CBSProfileModule>();
            FabCalendar = FabExecuter.Get<FabCalendar>();
            InAppPurchase = Get<CBSInAppPurchaseModule>();
        }

        /// <summary>
        /// Get list of all available calendars for profile
        /// </summary>
        /// <param name="result"></param>
        public void GetAllAvailableCalendars(Action<CBSGetAllCalendarsResult> result)
        {
            string profileID = Profile.ProfileID;

            FabCalendar.GetAllAvailableCalendars(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetAllCalendarsResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionAllCalendarsResult>();
                    var instances = functionResult.Instances;

                    result?.Invoke(new CBSGetAllCalendarsResult
                    {
                        IsSuccess = true,
                        Instances = instances
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetAllCalendarsResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get information about the status of the calendar by id. Also get a list of all calendar rewards.
        /// </summary>
        /// <param name="result"></param>
        public void GetCalendarByID(string calendarID, Action<CBSGetCalendarResult> result)
        {
            string profileID = Profile.ProfileID;

            FabCalendar.GetCalendarByID(profileID, calendarID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetCalendarResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<CalendarInstance>();
                    var instance = functionResult;

                    result?.Invoke(new CBSGetCalendarResult
                    {
                        IsSuccess = true,
                        Calendar = instance
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetCalendarResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Pickup calendar reward.
        /// </summary>
        /// <param name="result"></param>
        public void PickupReward(string calendarID, int positionIndex, Action<CBSPickupCalendarReward> result)
        {
            string profileID = Profile.ProfileID;

            FabCalendar.PickupReward(profileID, calendarID, positionIndex, onCollect =>
            {
                var cbsError = onCollect.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSPickupCalendarReward
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onCollect.GetResult<FunctionPickupCalendarRewardResult>();
                    var rewardResult = functionResult.RewardResult;
                    var newPosition = functionResult.UpdatedPosition;
                    var reward = rewardResult.OriginReward;

                    if (rewardResult != null && reward != null)
                    {
                        var currencies = rewardResult.GrantedCurrencies;
                        if (currencies != null)
                        {
                            var codes = currencies.Select(x => x.Key).ToArray();
                            Get<CBSCurrencyModule>().ChangeRequest(codes);
                        }

                        var grantedInstances = rewardResult.GrantedInstances;
                        if (grantedInstances != null && grantedInstances.Count > 0)
                        {
                            var inventoryItems = grantedInstances.Select(x => x.ToCBSInventoryItem()).ToList();
                            Get<CBSInventoryModule>().AddRequest(inventoryItems);
                        }
                    }

                    result?.Invoke(new CBSPickupCalendarReward
                    {
                        IsSuccess = true,
                        RewardResult = rewardResult,
                        UpdatedPosition = newPosition
                    });

                    OnRewardCollected?.Invoke(rewardResult);
                }
            }, onError =>
            {
                result?.Invoke(new CBSPickupCalendarReward
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                });
            });
        }

        /// <summary>
        /// Reset caledar states for current profile.
        /// </summary>
        /// <param name="result"></param>
        public void ResetCalendar(string calendarID, Action<CBSResetCalendarResult> result)
        {
            string profileID = Profile.ProfileID;

            FabCalendar.ResetCalendar(profileID, calendarID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSResetCalendarResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<CalendarInstance>();
                    var instance = functionResult;

                    OnCalendarReseted?.Invoke(instance);

                    result?.Invoke(new CBSResetCalendarResult
                    {
                        IsSuccess = true,
                        NewCalendar = instance
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSResetCalendarResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get the amount of rewards profile can pickup today
        /// </summary>
        /// <param name="result"></param>
        public void GetCalendarBadge(Action<CBSBadgeResult> result)
        {
            var profileID = Profile.ProfileID;

            FabCalendar.GetCalendarBadge(profileID, onReset =>
            {
                var cbsError = onReset.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSBadgeResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onReset.GetResult<FunctionBadgeResult>();
                    var badgeCount = functionResult.Count;

                    result?.Invoke(new CBSBadgeResult
                    {
                        IsSuccess = true,
                        Count = badgeCount
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSBadgeResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Grant calendar instance to profile. Only work when "Activation" equal "BY_PURCHASE"
        /// </summary>
        /// <param name="result"></param>
        public void GrantCalendar(string calendarID, Action<CBSGrantCalendarResult> result)
        {
            string profileID = Profile.ProfileID;

            FabCalendar.GrantCalendar(profileID, calendarID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGrantCalendarResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<CalendarInstance>();
                    var instance = functionResult;

                    OnCalendarGranted?.Invoke(instance);

                    result?.Invoke(new CBSGrantCalendarResult
                    {
                        IsSuccess = true,
                        NewCalendar = instance
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGrantCalendarResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Purchase calendar with currencies.
        /// </summary>
        /// <param name="calendarID"></param>
        /// <param name="result"></param>
        public void PurchaseCalendar(string calendarID, Action<CBSPurchaseCalendarResult> result)
        {
            string profileID = Profile.ProfileID;

            FabCalendar.PrePurchaseValidation(profileID, calendarID, onValidate =>
            {
                var cbsError = onValidate.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSPurchaseCalendarResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onValidate.GetResult<FunctionCalendarPurchaseValidationResult>();
                    var price = functionResult.Price;
                    var currencyCode = price.CurrencyID;
                    var currencyValue = price.CurrencyValue;

                    FabCalendar.PurchaseCalendar(calendarID, currencyCode, currencyValue, onPurchase =>
                    {
                        GetCalendarByID(calendarID, onGet =>
                        {
                            if (onGet.IsSuccess)
                            {
                                var calendarInstance = onGet.Calendar;
                                Get<CBSCurrencyModule>().ChangeRequest(currencyCode);
                                OnCalendarPurchased?.Invoke(calendarInstance);
                                result?.Invoke(new CBSPurchaseCalendarResult
                                {
                                    IsSuccess = true,
                                    PurchasedInstance = calendarInstance,
                                    PriceCode = currencyCode,
                                    PriceValue = currencyValue
                                });
                            }
                            else
                            {
                                result?.Invoke(new CBSPurchaseCalendarResult
                                {
                                    IsSuccess = false,
                                    Error = onGet.Error
                                });
                            }
                        });
                    }, onFailed =>
                    {
                        result?.Invoke(new CBSPurchaseCalendarResult
                        {
                            IsSuccess = false,
                            Error = CBSError.FromTemplate(onFailed)
                        });
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSPurchaseCalendarResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Purchase calendar with real money.
        /// </summary>
        /// <param name="calendarID"></param>
        /// <param name="result"></param>
        public void PurchaseCalendarWithRM(string calendarID, Action<CBSPurchaseCalendarWithRMResult> result)
        {
            string profileID = Profile.ProfileID;

            FabCalendar.PrePurchaseValidation(profileID, calendarID, onValidate =>
            {
                var cbsError = onValidate.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSPurchaseCalendarWithRMResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onValidate.GetResult<FunctionCalendarPurchaseValidationResult>();

                    InAppPurchase.PurchaseItem(calendarID, CatalogKeys.CalendarCatalogID, onPurchase =>
                    {
                        if (onPurchase.Error != null)
                        {
                            result?.Invoke(new CBSPurchaseCalendarWithRMResult
                            {
                                IsSuccess = false,
                                Error = onPurchase.Error
                            });
                        }
                        else
                        {
                            var transactionID = onPurchase.TransactionID;
                            GetCalendarByID(calendarID, onGet =>
                            {
                                if (onGet.IsSuccess)
                                {
                                    var calendarInstance = onGet.Calendar;
                                    OnCalendarPurchased?.Invoke(calendarInstance);
                                    result?.Invoke(new CBSPurchaseCalendarWithRMResult
                                    {
                                        IsSuccess = true,
                                        PurchasedInstance = calendarInstance,
                                        TransactionID = transactionID
                                    });
                                }
                                else
                                {
                                    result?.Invoke(new CBSPurchaseCalendarWithRMResult
                                    {
                                        IsSuccess = false,
                                        Error = onGet.Error
                                    });
                                }
                            });
                        }
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSPurchaseCalendarWithRMResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }
    }
}
