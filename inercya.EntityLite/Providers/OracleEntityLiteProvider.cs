﻿using inercya.EntityLite.Builders;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;

namespace inercya.EntityLite.Providers
{
    public class OracleEntityLiteProvider : EntityLiteProvider
    {
        public const string ProviderName = "Oracle.DataAccess.Client";
        private readonly DataService DataService;

        public OracleEntityLiteProvider(DataService dataService)
        {
            this.DataService = dataService;
            if (DataService.ProviderName != ProviderName)
            {
                throw new InvalidOperationException(this.GetType().Name + " is for " + ProviderName + ". Not for " + DataService.ProviderName);
            }
        }


        public override string ParameterPrefix
        {
            get { return ":"; }
        }


        public override AutoGeneratedFieldFetchMode AutoGeneratedFieldFetchMode
        {
            get { return AutoGeneratedFieldFetchMode.OutputParameter; }
        }

        public override string GetPagedQuery(AbstractQueryBuilder builder, DbCommand selectCommand, ref int paramIndex, int fromRowIndex, int toRowIndex)
        {
            /*
SELECT *
FROM (
  SELECT od.*, rownum AS RowNumber__
  FROM order_details od
) T
WHERE RowNumber__ between 10 and 19;  
             */

            var commandText = new StringBuilder();
            commandText.Append("\nSELECT ").Append(builder.GetColumnList()).Append("\n")
                       .Append("FROM (\n")
                       .Append("SELECT IT.*, rownum AS row_number__\n")
                       .Append("FROM ").Append(builder.GetFromClauseContent(selectCommand, ref paramIndex)).Append(" IT\n");
            bool hasWhereClause = builder.QueryLite.Filter != null && !builder.QueryLite.Filter.IsEmpty();
            if (hasWhereClause)
            {
                commandText.Append("\nWHERE\n    ").Append(builder.GetFilter(selectCommand, ref paramIndex, builder.QueryLite.Filter));
            }
            commandText.Append("\n) T\n");
            IDbDataParameter fromParameter = builder.CreateIn32Parameter(fromRowIndex + 1, ref paramIndex);
            selectCommand.Parameters.Add(fromParameter);
            IDbDataParameter toParameter = builder.CreateIn32Parameter(toRowIndex + 1, ref paramIndex);
            selectCommand.Parameters.Add(toParameter);
            commandText.Append("WHERE row_number__ BETWEEN ")
                .Append(fromParameter.ParameterName)
                .Append(" AND ").Append(toParameter.ParameterName);
            return commandText.ToString();
        }

        public override string SequenceVariable
        {
            get
            {
                return "id_seq_$var$";
            }
        }

        protected override DbCommand GenerateInsertCommandWithAutogeneratedField(CommandBuilder commandBuilder, object entity, EntityMetadata entityMetadata)
        {
            var cmd = commandBuilder.DataService.DbProviderFactory.CreateCommand();
            StringBuilder commandText = new StringBuilder();
            commandText.Append(string.Format(@"
DECLARE
    {0} NUMERIC(18);
BEGIN
    {0} := {1}.nextval;", SequenceVariable, entityMetadata.GetFullSequenceName(commandBuilder.DataService.EntityLiteProvider.DefaultSchema)));
            commandBuilder.AppendInsertStatement(entity, cmd, commandText);
            commandText.Append(@";
    :id_seq_$param$ := {0};
END;");
            IDbDataParameter idp = cmd.CreateParameter();
            idp.ParameterName = ":id_seq_$param$";
            idp.Direction = ParameterDirection.Output;
            idp.DbType = DbType.Int64;
            cmd.Parameters.Add(idp);
            cmd.CommandText = commandText.ToString();
            return cmd;
        }

        protected override void AppendGetAutoincrementField(StringBuilder commandText, EntityMetadata entityMetadata)
        {
            throw new NotImplementedException();
        }
    }
}