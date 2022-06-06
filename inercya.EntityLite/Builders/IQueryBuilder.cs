﻿/*
Copyright 2014 i-nercya intelligent software

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Common;

namespace inercya.EntityLite.Builders
{
    public interface IQueryBuilder
    {
        string GetSelectQuery(DbCommand cmd, ref int paramIndex, int indentation);
        string GetSelectQuery(DbCommand cmd, ref int paramIndex, int fromRowIndex, int toRowIndex, int indentation);
        string GetCountQuery(DbCommand cmd, ref int paramIndex);
        string GetAnyQuery(DbCommand cmd, ref int paramIndex);
        string GetDeleteQuery(DbCommand cmd, ref int paramIndex, int indentation);
        string GetSelectIntoQuery(DbCommand cmd, ref int paramIndex, int indentation, string destinationTableName);
        string GetInsertIntoQuery(DbCommand cmd, ref int paramIndex, int indentation, string destinationTableName, string[] columnNames);
        IQueryLite QueryLite { get; }
        string[] GetColumns(Type entityType, IList<string> propertyNames);
    }
}
