using Azure.Data.Tables;
using CBS.Models;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CBS
{
    public class TableEventsAssistant
    {
        private static readonly string EventLogTableID = "CBSEventsLogsTable";
        private static readonly string RawDataKey = "RawData";
        private static readonly int MaxLogsHistory = 100;

        public static async Task<ExecuteResult<FunctionEmptyResult>> SendLogAsync(EventExecutionLog log)
        {
            var partitionKey = TicksKey();
            var entity = new TableEntity();
            entity.PartitionKey = partitionKey;
            entity.RowKey = log.EventID;
            var rawData = JsonPlugin.ToJsonCompress(log);
            entity[RawDataKey] = rawData;
            var addResult = await StorageTable.AddEntityAsync(EventLogTableID, entity);
            if (addResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionEmptyResult>(addResult.Error);
            }
            return new ExecuteResult<FunctionEmptyResult>
            {
                Result = new FunctionEmptyResult()
            };
        }

        public static async Task<ExecuteResult<FunctionGetEventLogResult>> GetEventLogsAsync()
        {
            var getEntityResult = await StorageTable.GetTopFromTableAndSaveLastNAsync(EventLogTableID, MaxLogsHistory);
            if (getEntityResult.Error != null)
            {
                return ErrorHandler.ThrowError<FunctionGetEventLogResult>(getEntityResult.Error);
            }
            var entities = getEntityResult.Result;
            var logList = new List<EventExecutionLog>();
            foreach (var entity in entities)
            {
                logList.Add(ParseLogFromEntity(entity));
            }
            return new ExecuteResult<FunctionGetEventLogResult>
            {
                Result = new FunctionGetEventLogResult
                {
                    Logs = logList
                }
            };
        }

        private static EventExecutionLog ParseLogFromEntity(TableEntity entity)
        {
            var instanceID = entity.PartitionKey;
            var rawData = entity.GetString(RawDataKey);
            try
            {
                var eventLog = JsonPlugin.FromJsonDecompress<EventExecutionLog>(rawData);
                return eventLog;
            }
            catch {
                return null;
            }
        }

        public static string TicksKey()
        {
            return (DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks).ToString("d19");
        }
    }
}