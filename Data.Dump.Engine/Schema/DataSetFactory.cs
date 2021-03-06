﻿using Data.Dump.Schema.Mapping;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Data.Dump.Schema
{
    /// <inheritdoc cref="IDataSetFactory" />
    public class DataSetFactory : DataContainerFactoryBase, IDataSetFactory
    {
        public DataSetFactory(ITableDefinitionGenerator tableDefinitionGenerator)
            : base(tableDefinitionGenerator)
        {
        }

        private static void ClearDataSetTables(DataSet set)
        {
            var tables = new DataTable[set.Tables.Count];
            set.Tables.CopyTo(tables, 0);

            foreach (DataTable table in tables)
            {
                table.Clear();
                set.Tables.Remove(table);
            }
        }

        private void HandleForeignKeyField<T>(IFieldSelectorWithForeignKey<T> idSelector, object root, DataTable table,
            ref EventHandler<RowCreatedEventArgs> rowCreated)
            where T : class
        {
            if (rowCreated != null)
            {
                RowCreated -= rowCreated;
                rowCreated = null;
            }

            if (idSelector == null) return;

            var column = EnsureForeignKeyColumn(table, idSelector.ForeignKeyName() ?? "Auto_ParentId", idSelector.ForeignKeyType);

            if (column != null)
            {
                RowCreated += (rowCreated = (sender, args) =>
                {
                    if ((args.Row?.Table.Columns.Contains(column.ColumnName) ?? false))
                    {
                        args.Row[column.ColumnName] = idSelector
                                .GetForeignKey(
                                    (args.Model as IForeignKeyContainer)?.GetForeignKeyModel() ?? root
                                ) ?? DBNull.Value;
                    }
                });
            }
        }

        private DataColumn EnsureForeignKeyColumn(DataTable table, string name, Type type)
        {
            var colName = TableDefinitionGenerator.GetValidName(name);
            if (table.Columns.Contains(colName))
            {
                return table.Columns[colName];
            }

            if (!TryAddColumn(table, name, type, out var column)) return null;

            column.AllowDBNull = true;
            return column;
        }

        public virtual IEnumerable<DataSet> Create<T>(
            IEnumerable<T> data, FieldSelectorCollection<T> fieldSelectors, int dumpEvery = 100000)
            where T : class
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            foreach (var tables in FillDataTables(data, fieldSelectors, dumpEvery))
            {
                var set = new DataSet();
                foreach (var table in tables)
                {
                    if (table != null)
                    {
                        set.Tables.Add(table);
                    }
                }

                yield return set;
                ClearDataSetTables(set);
            }
        }

        protected virtual IEnumerable<IEnumerable<DataTable>> FillDataTables<T>(
            IEnumerable<T> data, FieldSelectorCollection<T> fieldSelectors, int dumpEvery)
            where T : class
        {
            EventHandler<RowCreatedEventArgs> rowCreated = null;

            var tables = new Dictionary<string, DataTable>();

            foreach (var model in data)
            {
                foreach (var selector in fieldSelectors)
                {
                    var name = GetTableName(selector.FieldType, selector.TableName());
                    if (!tables.TryGetValue(name, out var table))
                    {
                        tables.Add(
                            name,
                            (table = GetDataTableSchema(selector.FieldType, selector.TableName()))
                        );

                    }

                    var value = selector.GetField(model);

                    HandleForeignKeyField(
                        selector as IFieldSelectorWithForeignKey<T>, 
                        model, 
                        table, 
                        ref rowCreated
                    );

                    var tempResults = FillDataTable(
                        selector.FieldType,
                        table,
                        value as IEnumerable ?? new[] { value },
                        dumpEvery
                    );

                    foreach (var result in tempResults)
                    {
                        if (tables.Values.Sum(x => x.Rows.Count) >= dumpEvery)
                        {
                            yield return tables.Values;
                        }
                    }
                }
            }

            if (rowCreated != null)
            {
                RowCreated -= rowCreated;
            }

            yield return tables.Values;
        }
    }
}