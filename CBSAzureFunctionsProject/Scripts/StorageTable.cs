using Azure.Data.Tables;
using CBS.Models;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace CBS
{
    public static class StorageTable
    {
        private static string TableConnectionString => BaseAzureModule.StorageConnectionString;

        private static readonly TableServiceClient ServiceClient;

        static StorageTable()
        {
            ServiceClient = new TableServiceClient(TableConnectionString);
        }

        public static async Task<ExecuteResult<List<TableEntity>>> QueryEntitiesAsync(string tableID, string [] keys, string queryFilter)
        {
            var client = new TableClient(TableConnectionString, tableID);
            try
            {
                var resultList = new List<TableEntity>();
                await client.CreateIfNotExistsAsync();
                var entityResult = client.QueryAsync<TableEntity>(filter: queryFilter, select: keys);
                await foreach (TableEntity qEntity in entityResult)
                {
                    resultList.Add(qEntity);
                }
                return new ExecuteResult<List<TableEntity>>
                {
                    Result = resultList
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<List<TableEntity>>(err);
            }
        }

        public static async Task<ExecuteResult<List<TableEntity>>> GetTableAsync(string tableID)
        {
            var client = new TableClient(TableConnectionString, tableID);
            try
            {
                await client.CreateIfNotExistsAsync();
                var queryResultsMaxPerPage = client.QueryAsync<TableEntity>();

                var entityList = new List<TableEntity>();
                await foreach (TableEntity qEntity in queryResultsMaxPerPage)
                {
                    entityList.Add(qEntity);
                }
                return new ExecuteResult<List<TableEntity>>{
                    Result = entityList
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<List<TableEntity>>(err);
            }
        }

        public static async Task<ExecuteResult<List<TableEntity>>> GetTopFromTableAsync(string tableID, int count, string filter = "", int? ttl = -1)
        {
            var client = new TableClient(TableConnectionString, tableID);
            try
            {
                await client.CreateIfNotExistsAsync();

                var queryResultsMaxPerPage = client.QueryAsync<TableEntity>(maxPerPage: count);
                var pages = queryResultsMaxPerPage.AsPages();
                //var entities = await queryResultsMaxPerPage.ToListAsync();

                var entityList = new List<TableEntity>();
                await foreach (var page in pages)
                {
                    foreach (var entity in page.Values) 
                    {
                        entityList.Add(entity);
                    }
                    break;
                }
                return new ExecuteResult<List<TableEntity>>{
                    Result = entityList
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<List<TableEntity>>(err);
            }
        }

        public static async Task<ExecuteResult<List<TableEntity>>> GetTopFromTableAndDeleteOutDatesAsync(string tableID, int count, int? ttl)
        {
            var client = new TableClient(TableConnectionString, tableID);
            try
            {
                var checkOutdated = ttl != null && ttl > 0;
                await client.CreateIfNotExistsAsync();

                var queryResultsMaxPerPage = client.QueryAsync<TableEntity>(maxPerPage: count);

                var entityList = new List<TableEntity>();
                var deleteList = new List<TableEntity>();
                var firstPage = false;

                string token = null;
                do
                {
                    await foreach (var page in queryResultsMaxPerPage.AsPages(token))
                    {
                        if (!firstPage)
                        {
                            foreach (var entity in page.Values) 
                            {
                                if (checkOutdated)
                                {
                                    var dateOfset = entity.Timestamp;
                                    var ticks = dateOfset.GetValueOrDefault().UtcDateTime.AddSeconds((int)ttl).Ticks;
                                    var limitTicks = BaseAzureModule.ServerTimeUTC.Ticks;
                                    if (ticks < limitTicks)
                                    {
                                        deleteList.Add(entity);
                                    }
                                    else
                                    {
                                        entityList.Add(entity);
                                    }
                                }
                                else
                                {
                                    entityList.Add(entity);
                                } 
                            }
                            firstPage = true;
                        }
                        else
                        {
                            foreach (var entity in page.Values) 
                            {
                                deleteList.Add(entity);
                            }
                        }
                        token = page.ContinuationToken;
                    }
                }
                while(!string.IsNullOrEmpty(token));

                var hasEntityToRemove = deleteList.Count > 0;
                if (hasEntityToRemove)
                {
                    var removeTaskList = new List<Task<ExecuteResult<Azure.Response>>>();
                    foreach (var entity in deleteList)
                    {
                        var removeTask = DeleteEntityAsync(tableID, entity);
                        removeTaskList.Add(removeTask);
                    }
                    await Task.WhenAll(removeTaskList);
                }
                
                return new ExecuteResult<List<TableEntity>>{
                    Result = entityList
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<List<TableEntity>>(err);
            }
        }

        public static async Task<ExecuteResult<List<TableEntity>>> GetAllEntitiesFromTableAsync(string tableID)
        {
            var countPerIteration = 100;
            var client = new TableClient(TableConnectionString, tableID);
            try
            {
                await client.CreateIfNotExistsAsync();

                var queryResultsMaxPerPage = client.QueryAsync<TableEntity>(maxPerPage: countPerIteration);

                var entityList = new List<TableEntity>();

                string token = null;
                do
                {
                    await foreach (var page in queryResultsMaxPerPage.AsPages(token))
                    {
                        foreach (var entity in page.Values) 
                        {
                            entityList.Add(entity);
                        }
                        token = page.ContinuationToken;
                    }
                }
                while(!string.IsNullOrEmpty(token));
                
                return new ExecuteResult<List<TableEntity>>{
                    Result = entityList
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<List<TableEntity>>(err);
            }
        }

        public static async Task<ExecuteResult<List<TableEntity>>> GetTopFromTableAndSaveLastNAsync(string tableID, int count)
        {
            var allEntitiesResult = await GetAllEntitiesFromTableAsync(tableID);
            if (allEntitiesResult.Error != null)
            {
                return ErrorHandler.ThrowError<List<TableEntity>>(allEntitiesResult.Error);
            }
            var allEntities = allEntitiesResult.Result ?? new List<TableEntity>();

            allEntities.Sort((x, y) => DateTime.Compare(x.Timestamp.GetValueOrDefault().DateTime, y.Timestamp.GetValueOrDefault().DateTime));
            allEntities.Reverse();
            try
            {
                var entityList = new List<TableEntity>();
                var deleteList = new List<TableEntity>();

                var loadCounter = count;

                foreach (var entity in allEntities) 
                {
                    if (loadCounter >= 0)
                    {
                        entityList.Add(entity);
                    }
                    else
                    {
                        deleteList.Add(entity);
                    }
                    loadCounter--;
                }

                var hasEntityToRemove = deleteList.Count > 0;
                if (hasEntityToRemove)
                {
                    var removeTaskList = new List<Task<ExecuteResult<Azure.Response>>>();
                    foreach (var entity in deleteList)
                    {
                        var removeTask = DeleteEntityAsync(tableID, entity);
                        removeTaskList.Add(removeTask);
                    }
                    await Task.WhenAll(removeTaskList);
                }
                
                return new ExecuteResult<List<TableEntity>>{
                    Result = entityList
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<List<TableEntity>>(err);
            }
        }

        public static async Task<ExecuteResult<FunctionEmptyResult>> RemoveOutdatedAndSaveLast(string tableID, int saveLast, int? ttl)
        {
            var checkCount = 100;
            var iterationCounter = 0;
            var client = new TableClient(TableConnectionString, tableID);
            try
            {
                var checkOutdated = ttl != null && ttl > 0;
                await client.CreateIfNotExistsAsync();

                var queryResultsMaxPerPage = client.QueryAsync<TableEntity>(maxPerPage: checkCount);

                var deleteList = new List<TableEntity>();

                string token = null;
                do
                {
                    await foreach (var page in queryResultsMaxPerPage.AsPages(token))
                    {
                        foreach (var entity in page.Values) 
                        {
                            if (checkOutdated && iterationCounter >= saveLast)
                            {
                                var dateOfset = entity.Timestamp;
                                var ticks = dateOfset.GetValueOrDefault().UtcDateTime.AddSeconds((int)ttl).Ticks;
                                var limitTicks = BaseAzureModule.ServerTimeUTC.Ticks;
                                if (ticks < limitTicks)
                                {
                                    deleteList.Add(entity);
                                }
                            }
                            iterationCounter++;
                        }
                        token = page.ContinuationToken;
                    }
                }
                while(!string.IsNullOrEmpty(token));

                var hasEntityToRemove = deleteList.Count > 0;
                if (hasEntityToRemove)
                {
                    var removeTaskList = new List<Task<ExecuteResult<Azure.Response>>>();
                    foreach (var entity in deleteList)
                    {
                        var removeTask = DeleteEntityAsync(tableID, entity);
                        removeTaskList.Add(removeTask);
                    }
                    await Task.WhenAll(removeTaskList);
                }
                
                return new ExecuteResult<FunctionEmptyResult>
                {
                    Result = new FunctionEmptyResult()
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<FunctionEmptyResult>(err);
            }
        }

        public static async Task<ExecuteResult<TableEntity>> GetFirstEntityAsync(string tableID, int? ttl = -1)
        {
            var client = new TableClient(TableConnectionString, tableID);
            try
            {
                await client.CreateIfNotExistsAsync();
                var entity = await client.QueryAsync<TableEntity>(maxPerPage: 1).FirstOrDefaultAsync();
                return new ExecuteResult<TableEntity>
                {
                    Result = entity
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<TableEntity>(err);
            }
        }

        public static async Task<ExecuteResult<EntityPageResult>> GetTableAsPagesAsync(string tableID, int maxPerPage, string [] keys, int page = 1)
        {
            var client = new TableClient(TableConnectionString, tableID);
            try
            {
                await client.CreateIfNotExistsAsync();
                var queryResultsMaxPerPage = client.QueryAsync<TableEntity>(maxPerPage: maxPerPage, select: keys);
                var entityList = new List<TableEntity>();
                var pageCounter = 0;
                await foreach (var p in queryResultsMaxPerPage.AsPages())
                {
                    pageCounter ++;
                    if (page == pageCounter)
                    {
                        var pageList = p.Values;
                        foreach (var qEntity in pageList)
                        {
                            entityList.Add(qEntity);
                        }
                    }
                }
                return new ExecuteResult<EntityPageResult>{
                    Result = new EntityPageResult
                    {
                        Entities = entityList,
                        Pages = pageCounter
                    }
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<EntityPageResult>(err);
            }
        }

        public static async Task<ExecuteResult<TableEntity>> GetEntityAsync(string tableID, string partKey, string rowKey, string [] keys, int ttl = -1)
        {
            var client = new TableClient(TableConnectionString, tableID);
            try
            {
                await client.CreateIfNotExistsAsync();
                var entityResult = await client.GetEntityAsync<TableEntity>(partKey, rowKey, keys);
                var entity = entityResult.Value;
                return new ExecuteResult<TableEntity>
                {
                    Result = entity
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<TableEntity>(err);
            }
        }

        public static async Task<ExecuteResult<Azure.Response>> UpsertEntityAsync(string tableID, TableEntity entity)
        {
            var client = new TableClient(TableConnectionString, tableID);
            try
            {
                await client.CreateIfNotExistsAsync();
                var respone = await client.UpsertEntityAsync(entity, TableUpdateMode.Merge);
                return new ExecuteResult<Azure.Response>
                {
                    Result = respone
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<Azure.Response>(err);
            }
        }

        public static async Task<ExecuteResult<Azure.Response>> UpdateEntityAsync(string tableID, TableEntity entity)
        {
            var client = new TableClient(TableConnectionString, tableID);
            try
            {
                await client.CreateIfNotExistsAsync();
                var respone = await client.UpdateEntityAsync(entity, Azure.ETag.All);
                return new ExecuteResult<Azure.Response>
                {
                    Result = respone
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<Azure.Response>(err);
            }
        }

        public static async Task<ExecuteResult<Azure.Response>> AddEntityAsync(string tableID, TableEntity entity, int? ttl = null)
        {
            var client = new TableClient(TableConnectionString, tableID);
            try
            {
                await client.CreateIfNotExistsAsync();
                var respone = await client.AddEntityAsync(entity);
                return new ExecuteResult<Azure.Response>
                {
                    Result = respone
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<Azure.Response>(err);
            }
        }

        public static async Task<ExecuteResult<Azure.Response>> DeleteEntityAsync(string tableID, string rowKey, string partitionKey)
        {
            var client = new TableClient(TableConnectionString, tableID);
            try
            {
                await client.CreateIfNotExistsAsync();
                var respone = await client.DeleteEntityAsync(partitionKey, rowKey);
                return new ExecuteResult<Azure.Response>
                {
                    Result = respone
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<Azure.Response>(err);
            }
        }

        public static async Task<ExecuteResult<Azure.Response>> DeleteEntityAsync(string tableID, ITableEntity entity)
        {
            var client = new TableClient(TableConnectionString, tableID);
            try
            {
                var respone = await client.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
                return new ExecuteResult<Azure.Response>
                {
                    Result = respone
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<Azure.Response>(err);
            }
        }

        public static async Task<ExecuteResult<Azure.Response>> DeleteTableAsync(string tableID)
        {
            try
            {
                var respone = await ServiceClient.DeleteTableAsync(tableID);
                return new ExecuteResult<Azure.Response>
                {
                    Result = respone
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<Azure.Response>(err);
            }
        }

        // internal 
        public static TableClient GetTableClient(string tableID)
        {
            return new TableClient(TableConnectionString, tableID);
        }
    }
}