using System;
using PlayFab.ServerModels;

namespace CBS.Models
{
    public class ExecuteResult<T> where T : class
    {
        public CBSError Error;
        public T Result;
    }
}
