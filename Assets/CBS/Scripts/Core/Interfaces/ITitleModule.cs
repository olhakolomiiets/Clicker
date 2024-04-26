
using CBS.Models;
using System;
using System.Collections.Generic;

namespace CBS
{
    public interface ITitleModule
    {
        /// <summary>
        /// Get CBS Title Data by key
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="result"></param>
        void GetTitleDataByKey<T>(string key, Action<CBSGetTitleDataResult<T>> result) where T : TitleCustomData;
        /// <summary>
        /// Get all CBS Title Data
        /// </summary>
        /// <param name="result"></param>
        void GetAllTitleData(Action<CBSGetAllTitleDataResult> result);
        /// <summary>
        /// Update current CBS Title data cache from server
        /// </summary>
        /// <param name="result"></param>
        void UpdateCacheFromServer(Action<CBSBaseResult> result);
        /// <summary>
        /// Get CBS Title data by key from cache
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        T GetFromCacheByKey<T>(string key) where T : TitleCustomData;
        /// <summary>
        /// Get all CBS Title data from cache
        /// </summary>
        /// <returns></returns>
        Dictionary<string, CBSTitleData> GetAllFromCache();
    }
}
