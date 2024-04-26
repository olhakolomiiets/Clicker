using CBS.Models;
using CBS.Playfab;
using CBS.Utils;
using System;
using System.Collections.Generic;

namespace CBS
{
    public class CBSTitleDataModule : CBSModule, ITitleModule
    {
        private IFabTitle FabTitle { get; set; }

        private Dictionary<string, CBSTitleData> TitleCache { get; set; }

        protected override void Init()
        {
            FabTitle = FabExecuter.Get<FabTitleData>();
            TitleCache = new Dictionary<string, CBSTitleData>();
        }

        // CBS API

        /// <summary>
        /// Get CBS Title Data by key
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="result"></param>
        public void GetTitleDataByKey<T>(string key, Action<CBSGetTitleDataResult<T>> result) where T : TitleCustomData
        {
            FabTitle.GetTitleDataByKey(key, onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetTitleDataResult<T>
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionTitleDataByKeyResult>();
                    var dataKey = functionResult.Key;
                    var rawData = functionResult.RawData;
                    var cbsTitleData = JsonPlugin.FromJsonDecompress<CBSTitleData>(rawData);
                    var resultObject = cbsTitleData.GetCustomData<T>();

                    result?.Invoke(new CBSGetTitleDataResult<T>
                    {
                        IsSuccess = true,
                        Data = resultObject,
                        CBSTitleData = cbsTitleData
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetTitleDataResult<T>
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Get all CBS Title Data
        /// </summary>
        /// <param name="result"></param>
        public void GetAllTitleData(Action<CBSGetAllTitleDataResult> result)
        {
            FabTitle.GetAllTitleData(onGet =>
            {
                var cbsError = onGet.GetCBSError();
                if (cbsError != null)
                {
                    result?.Invoke(new CBSGetAllTitleDataResult
                    {
                        IsSuccess = false,
                        Error = cbsError
                    });
                }
                else
                {
                    var functionResult = onGet.GetResult<FunctionGetAllTitleDataResult>();
                    var dataDict = functionResult.Data;
                    var dataContainer = new TitleDataContainer(dataDict);

                    result?.Invoke(new CBSGetAllTitleDataResult
                    {
                        IsSuccess = true,
                        DataDictionary = dataContainer.GetAll()
                    });
                }
            }, onFailed =>
            {
                result?.Invoke(new CBSGetAllTitleDataResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onFailed)
                });
            });
        }

        /// <summary>
        /// Update current CBS Title data cache from server
        /// </summary>
        /// <param name="result"></param>
        public void UpdateCacheFromServer(Action<CBSBaseResult> result)
        {
            GetAllTitleData(onGet =>
            {
                if (onGet.IsSuccess)
                {
                    var data = onGet.DataDictionary;
                    TitleCache = data;
                }
                else
                {
                    result?.Invoke(new CBSBaseResult
                    {
                        IsSuccess = false,
                        Error = onGet.Error
                    });
                }
            });
        }

        /// <summary>
        /// Get CBS Title data by key from cache
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T GetFromCacheByKey<T>(string key) where T : TitleCustomData
        {
            if (TitleCache.ContainsKey(key))
            {
                var cbsTitleData = TitleCache[key];
                return cbsTitleData.GetCustomData<T>();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Get all CBS Title data from cache
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, CBSTitleData> GetAllFromCache()
        {
            return TitleCache;
        }

        // intarnal

        internal void ParseData(Dictionary<string, string> data)
        {
            var titleContainer = new TitleDataContainer(data);
            TitleCache = titleContainer.GetAll();
        }

        private void ClearCache()
        {
            TitleCache.Clear();
        }

        protected override void OnLogout()
        {
            ClearCache();
        }
    }
}

