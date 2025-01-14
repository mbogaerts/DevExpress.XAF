﻿using System;
using System.Linq;
using System.Linq.Expressions;
using DevExpress.EasyTest.Framework;
using DevExpress.EasyTest.Framework.Commands;
using Xpand.Extensions.XAF.ObjectExtensions;

namespace Xpand.TestsLib.EasyTest.Commands{
    public class SelectObjectsCommand<TObject,TColumn> : SelectObjectsCommand{
        public SelectObjectsCommand(Expression<Func<TObject, object>> tableSelector, Expression<Func<TColumn, object>> column, params string[] rows) :
            base(tableSelector.MemberExpressionCaption(), column.MemberExpressionCaption(), rows){
        }
    }
    public class SelectObjectsCommand<T> : SelectObjectsCommand{
        public SelectObjectsCommand(Expression<Func<T, object>> tableSelector, string column, string[] rows) :
            base(tableSelector.MemberExpressionCaption(), column, rows){
        }
    
        public SelectObjectsCommand(Expression<Func<T, object>> column, string[] rows) : base(typeof(T).Name, column.MemberExpressionCaption(), rows){
        }
    }

    public class SelectObjectsCommand : EasyTestCommand{
        private readonly Command _command;
        public const string Name = "SelectObjects";

        public SelectObjectsCommand(MainParameter mainParameter=null){
            Parameters.Add(new Parameter("SelectAll = True"));
            _command = this.ConnvertTo<ExecuteTableActionCommand>();
            if (mainParameter != null) _command.Parameters.MainParameter = mainParameter;
        }

        public SelectObjectsCommand(string tableName,string column,params string[] rows):this(column,rows){
            _command.Parameters.MainParameter = new MainParameter(tableName.CompoundName());
            
        }
        public SelectObjectsCommand(string column,params string[] rows){
            Parameters.Add(new Parameter($"Columns = {column}"));
            Parameters.AddRange(rows.Select(s => new Parameter($"Row = {s}")));
            _command = this.ConnvertTo<SelectRecordsCommand>();
        }

        protected override void ExecuteCore(ICommandAdapter adapter){
            adapter.Execute(_command);
        }
    }
}