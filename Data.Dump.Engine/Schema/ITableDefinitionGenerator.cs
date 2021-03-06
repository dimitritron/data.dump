﻿using System.Data;

namespace Data.Dump.Schema
{
    public interface ITableDefinitionGenerator
    {
        string GetTableDefinition(DataTable table);
        string GetValidName(string objectName);
        string GetColumnDefinition(DataColumn column);
        string GetColumnDefinition(DataTable table);
        string GetDbType(DataColumn column);
    }
}