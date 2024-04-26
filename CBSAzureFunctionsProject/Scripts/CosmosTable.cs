using Azure.Data.Tables;
using CBS.Models;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace CBS
{
    public static class CosmosTable
    {
        private static string TableConnectionString
        {
            get
            {
                var cosmosConnectionString = BaseAzureModule.CosmosConnectionString;
                if (string.IsNullOrEmpty(cosmosConnectionString))
                {
                    return BaseAzureModule.StorageConnectionString;
                }
                return cosmosConnectionString;
            }
        }

        private static readonly TableServiceClient ServiceClient;

        static CosmosTable()
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
                //var page = queryResultsMaxPerPage.AsPages();
                var entities = await queryResultsMaxPerPage.ToListAsync();

                /*var entityList = new List<TableEntity>();
                foreach (var qEntity in entities.Values)
                {
                    entityList.Add(qEntity);
                }*/
                return new ExecuteResult<List<TableEntity>>{
                    Result = entities
                };
            }
            catch (Exception err)
            {
                return ErrorHandler.ThrowTableError<List<TableEntity>>(err);
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

        // internal 
        public static TableClient GetTableClient(string tableID)
        {
            return new TableClient(TableConnectionString, tableID);
        }
    }
}