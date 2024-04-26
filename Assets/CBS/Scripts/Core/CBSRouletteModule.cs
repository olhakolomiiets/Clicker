using CBS.Models;
using CBS.Playfab;
using CBS.Utils;
using System;
using System.Linq;

namespace CBS
{
    public class CBSRouletteModule : CBSModule, IRoulette
    {
        private IFabRoulette FabRoulette { get; set; }
        private IProfile Profile { get; set; }

        protected override void Init()
        {
            FabRoulette = FabExecuter.Get<FabRoulette>();
            Profile = Get<CBSProfileModule>();
        }

        /// <summary>
        /// Get list of all roulette positions
        /// </summary>
        /// <param name="result"></param>
        public void GetRouletteTable(Action<CBSGetRouletteTableResult> result)
        {
            var profileID = Profile.ProfileID;

            FabRoulette.GetRouletteTable(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetRouletteTableResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<RouletteTable>();

                    result?.Invoke(new CBSGetRouletteTableResult
                    {
                        IsSuccess = true,
                        Table = functionResult
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetRouletteTableResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Start spin roulette and get spin result
        /// </summary>
        /// <param name="result"></param>
        public void Spin(Action<CBSSpinRouletteResult> result)
        {
            var profileID = Profile.ProfileID;

            FabRoulette.SpinRoulette(profileID, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSSpinRouletteResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionSpinRouletteResult>();
                    var grantResult = functionResult.RewardResult;
                    var dropedPosition = functionResult.DroppedPosition;

                    if (functionResult != null && grantResult != null)
                    {
                        var currencies = grantResult.GrantedCurrencies;
                        if (currencies != null)
                        {
                            var codes = currencies.Select(x => x.Key).ToArray();
                            Get<CBSCurrencyModule>().ChangeRequest(codes);
                        }

                        var grantedInstances = grantResult.GrantedInstances;
                        if (grantedInstances != null && grantedInstances.Count > 0)
                        {
                            var inventoryItems = grantedInstances.Select(x => x.ToCBSInventoryItem()).ToList();
                            Get<CBSInventoryModule>().AddRequest(inventoryItems);
                        }
                    }

                    result?.Invoke(new CBSSpinRouletteResult
                    {
                        IsSuccess = true,
                        RewardResult = grantResult,
                        Position = dropedPosition
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSSpinRouletteResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }
    }
}

