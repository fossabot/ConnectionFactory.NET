﻿using System;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Transactions;
using IsolationLevel = System.Data.IsolationLevel;

namespace ConnectionFactory
{
   public class CfConnection : IDisposable
   {
      private readonly DbProviderFactory _dbProvider;
      private DbConnection _conn;
      private DbTransaction _tran;
      private long _tranOpenCount;

      internal enum TransactionType
      {
         TransactionOpen = 1,
         TransactionCommit = 2,
         TransactionRollback = 3
      }

      [System.Diagnostics.DebuggerStepThrough]
      public CfConnection(string connectionName)
      {
         Configuration = ConfigurationManager.ConnectionStrings[connectionName];
         _dbProvider = DbProviderFactories.GetFactory(Configuration.ProviderName);
      }

      [System.Diagnostics.DebuggerStepThrough]
      public void Dispose()
      {
         CloseFactoryConnection();
         //_dbProvider = null;
         if (_tran != null) _tran.Dispose();
      }

      [System.Diagnostics.DebuggerStepThrough]
      public void EstablishFactoryConnection()
      {
         try
         {
            if (null == _conn)
               _conn = _dbProvider.CreateConnection();

            if (_conn == null || !_conn.State.Equals(ConnectionState.Closed)) return;

            _conn.ConnectionString = Configuration.ConnectionString;

            if (Transaction.Current == null)
            {
               _conn.Open();
            }

            _tranOpenCount = 0;
         }
         catch (DbException dbe)
         {
            throw new CfException("Não foi possível se conectar ao banco de dados", dbe);
         }
         catch (Exception ex)
         {
            throw new CfException("Não foi possível se conectar ao banco de dados", ex);
         }
      }

      [System.Diagnostics.DebuggerStepThrough]
      private void CloseFactoryConnection()
      {
         if (_conn == null) return;

         if (!_conn.State.Equals(ConnectionState.Closed))
         {
            if (_tranOpenCount > 0)
            {
               TransactionHandler(TransactionType.TransactionRollback);
            }
            _conn.Close();
         }
         _tranOpenCount = 0;
         //_conn.Dispose();
         //_conn = null;

      }

      [System.Diagnostics.DebuggerStepThrough]
      internal void TransactionHandler(TransactionType transactionType, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
      {
         EstablishFactoryConnection();

         switch (transactionType)
         {
            case TransactionType.TransactionOpen:
               _tran = _conn.BeginTransaction(isolationLevel);
               _tranOpenCount++;
               break;
            case TransactionType.TransactionCommit:
               _tran.Commit();
               _tranOpenCount--;
               break;
            case TransactionType.TransactionRollback:
               _tran.Rollback();
               _tranOpenCount--;
               break;
            default:
               throw new ArgumentOutOfRangeException("transactionType");
         }
      }

      public ConnectionStringSettings Configuration { get; private set; }

      public ConnectionState State
      {
         get { return _conn.State; }
      }

      internal DbCommand CreateDbCommand()
      {
         var cmd = _conn.CreateCommand();
         if (cmd.Connection.State == ConnectionState.Closed)
         {
            cmd.Connection.Open();
         }
         //var cmd =_dbProvider.CreateCommand();
         //cmd.Connection = _conn;
         if (_tranOpenCount > 0)
         {
            cmd.Transaction = _tran;
         }
         return cmd;
      }

      [System.Diagnostics.DebuggerStepThrough]
      public CfCommand CreateCfCommand()
      {
         var cfConnection = this;
         return new CfCommand(ref cfConnection);
      }

      [System.Diagnostics.DebuggerStepThrough]
      public DbDataAdapter CreateDataAdapter()
      {
         var dataAdp = _dbProvider.CreateDataAdapter();
         return dataAdp;
      }

      [System.Diagnostics.DebuggerStepThrough]
      public CfTransaction CreateTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
      {
         var cfConnection = this;
         return new CfTransaction(ref cfConnection, isolationLevel);
      }
   }
}
