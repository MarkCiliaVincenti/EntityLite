﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using inercya.EntityLite;

namespace inercya.EntityLite.Extensions
{
    public static class PivotExtensions
    {
        public static DataTable Pivot(this IDataReader reader, PivotDef pivotDef, Comparison<PivotedColumn> pivotedColumnComparison)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (pivotDef == null) throw new ArgumentNullException(nameof(pivotDef));
            if (pivotedColumnComparison == null) throw new ArgumentNullException(nameof(pivotedColumnComparison));
            if (pivotDef.UnpivotedColumnNames == null) throw new ArgumentException("UnpivotedColumns must not be null");
            if (pivotDef.PivotTransforms == null) throw new ArgumentException("PivotColumns must not be null");
            if (pivotDef.PivotTransforms.Any(x => x == null)) throw new ArgumentException("All PivotColums mut be not null");
            var builder = new SimplePivotBuilder(pivotDef, reader, pivotedColumnComparison);
            return builder.Build();
        }

        public static DataTable Pivot(this IDataReader reader, PivotDef pivotDef)
        {
            return Pivot(reader, pivotDef, DefaultPivotedColumnComparison);
        }  

        internal static int DefaultPivotedColumnComparison(PivotedColumn c1, PivotedColumn c2)
        {
            int indexComparisonResult = c1.PivotTransformIndex.CompareTo(c2.PivotTransformIndex);
            if (indexComparisonResult != 0) return indexComparisonResult;
            var isC1Null = c1.PivotColumnValue == null || c1.PivotColumnValue == DBNull.Value;
            var isC2Null = c2.PivotColumnValue == null || c2.PivotColumnValue == DBNull.Value;

            if (isC1Null && isC2Null) return 0;
            if (isC1Null) return -1;
            if (isC2Null) return 1;

            var comparable = c1.PivotColumnValue as IComparable;
            if (comparable != null) return comparable.CompareTo(c2.PivotColumnValue);
            return string.Compare(c1.ColumnName, c2.ColumnName, StringComparison.OrdinalIgnoreCase);
        }






        private class SimplePivotBuilder
        {

            private readonly Comparison<PivotedColumn> PivotedColumnComparison;
            private readonly PivotDef PivotDef;
            private readonly IDataReader Reader;

            private Record PreviousKey;
            private Record CurrentKey;
            private DataRow CurrentRow;

            private int[] KeyFieldsMappings;
            private int[] PivotColumnsMappings;
            private int[] ValueColumnsMappings;

            private readonly List<PivotedColumn> PivotedColumns = new List<PivotedColumn>();

            public SimplePivotBuilder(PivotDef pivotDef, IDataReader reader, Comparison<PivotedColumn> pivotedColumnComparison)
            {
                this.PivotedColumnComparison = pivotedColumnComparison;
                this.PivotDef = pivotDef;
                this.Reader = reader;
            }

            private void ReadCurrentKey()
            {
                if (CurrentKey == null)
                {
                    CurrentKey = new Record(this.PivotDef.UnpivotedColumnNames.Length);
                }
                for (int i = 0; i < this.PivotDef.UnpivotedColumnNames.Length; i++)
                {
                    this.CurrentKey[i] = Reader[this.KeyFieldsMappings[i]];
                }
            }

            private void SwapPreviousAndCurrentKeys()
            {
                Record aux = PreviousKey;
                PreviousKey = CurrentKey;
                CurrentKey = aux;
            }

            private int GetFieldIndex(string fieldName)
            {
                int fieldIndex = 0;
                if (!readerFields.TryGetValue(fieldName, out fieldIndex) && !pascalReaderFields.TryGetValue(fieldName, out fieldIndex))
                {
                    string localizedFieldName = CurrentLanguageService.GetSufixedLocalizedFieldName(fieldName);
                    if (!readerFields.TryGetValue(localizedFieldName, out fieldIndex) && !pascalReaderFields.TryGetValue(localizedFieldName, out fieldIndex))
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Field {0} not found in data reader", fieldName));
                    }
                }
                return fieldIndex;
            }

            private Dictionary<string, int> readerFields;
            private Dictionary<string, int> pascalReaderFields;

            private void BuildMappings()
            {
                readerFields = new Dictionary<string, int>(Reader.FieldCount, StringComparer.InvariantCultureIgnoreCase);
                pascalReaderFields = new Dictionary<string, int>(Reader.FieldCount, StringComparer.InvariantCultureIgnoreCase);
                for (int i = 0; i < Reader.FieldCount; i++)
                {
                    string fieldName = Reader.GetName(i);
                    if (!string.IsNullOrEmpty(fieldName))
                    {
                        readerFields[fieldName] = i;
                        pascalReaderFields[fieldName.ToPascalNamingConvention()] = i;
                    }
                }
                this.KeyFieldsMappings = new int[this.PivotDef.UnpivotedColumnNames.Length];
                for (int i = 0; i < this.PivotDef.UnpivotedColumnNames.Length; i++)
                {
                    this.KeyFieldsMappings[i] = GetFieldIndex(this.PivotDef.UnpivotedColumnNames[i]);
                }
                this.PivotColumnsMappings = new int[this.PivotDef.PivotTransforms.Length];
                this.ValueColumnsMappings = new int[this.PivotDef.PivotTransforms.Length];
                for (int i = 0; i < this.PivotDef.PivotTransforms.Length; i++)
                {
                    var pivotColumn = this.PivotDef.PivotTransforms[i];
                    this.PivotColumnsMappings[i] = GetFieldIndex(pivotColumn.PivotColumnName);
                    this.ValueColumnsMappings[i] = GetFieldIndex(pivotColumn.ValueColumnName);
                }
                readerFields = null;

                pascalReaderFields = null;
            }

            public DataTable Build()
            {
                BuildMappings();

                var targetTable = new DataTable();
                targetTable.BeginLoadData();
                AddUnpivotedColumnsColumsToDataTable(targetTable);

                while (Reader.Read())
                {
                    ReadCurrentKey();
                    AddNewRowIfNeeded(targetTable);
                    SetColumnValues(targetTable);
                }
                if (CurrentRow != null) targetTable.Rows.Add(CurrentRow);

                ReorderColumns(targetTable);

                targetTable.AcceptChanges();
                targetTable.EndLoadData();
                return targetTable;
            }

            private void ReorderColumns(DataTable targetTable)
            {
                this.PivotedColumns.Sort(this.PivotedColumnComparison);
                int ordinalBase = this.PivotDef.UnpivotedColumnNames.Length;
                for (int i = 0; i < this.PivotedColumns.Count; i++)
                {
                    var pivotedColumn = this.PivotedColumns[i];
                    targetTable.Columns[pivotedColumn.ColumnName].SetOrdinal(ordinalBase + i);
                }
            }

            private void SetColumnValues(DataTable targetTable)
            {
                for (int i = 0; i < this.PivotDef.PivotTransforms.Length; i++)
                {
                    var pivotColumn = this.PivotDef.PivotTransforms[i];
                    object pivotColumnValue = Reader[this.PivotColumnsMappings[i]];
                    string targetColumnName = pivotColumn.GetPivotedColumnName(pivotColumnValue);
                    DataColumn targetColumn = targetTable.Columns[targetColumnName];
                    if (targetColumn == null)
                    {
                        targetColumn = targetTable.Columns.Add(targetColumnName, Reader.GetFieldType(this.ValueColumnsMappings[i]));
                        PivotedColumns.Add(new PivotedColumn(pivotColumn.PivotColumnName, pivotColumnValue, i, targetColumnName));
                    }
                    CurrentRow[targetColumn] = Reader[this.ValueColumnsMappings[i]];
                }
            }

            private void AddNewRowIfNeeded(DataTable targetTable)
            {
                if (!Record.Equals(this.PreviousKey, this.CurrentKey))
                {
                    if (CurrentRow != null) targetTable.Rows.Add(CurrentRow);

                    CurrentRow = targetTable.NewRow();
                    for (int i = 0; i < this.CurrentKey.FieldCount; i++)
                    {
                        CurrentRow[i] = this.CurrentKey[i];
                    }
                    SwapPreviousAndCurrentKeys();
                }
            }

            private void AddUnpivotedColumnsColumsToDataTable(DataTable targetTable)
            {
                for (int i = 0; i < this.PivotDef.UnpivotedColumnNames.Length; i++)
                {
                    string keyField = this.PivotDef.UnpivotedColumnNames[i];
                    targetTable.Columns.Add(keyField, Reader.GetFieldType(this.KeyFieldsMappings[i]));
                }
            }
        }
    }
}
