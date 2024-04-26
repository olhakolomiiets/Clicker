using Azure.Data.Tables;
using System.Collections.Generic;

namespace CBS.Models
{
    public class EntityPageResult
    {
        public List<TableEntity> Entities;
        public int Pages;
    }
}