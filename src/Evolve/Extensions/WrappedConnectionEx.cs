﻿using System;
using System.Collections.Generic;
using System.Data;
using Evolve.Connection;
using Evolve.Dialect;
using Evolve.Utilities;

namespace Evolve
{
    public static class WrappedConnectionEx
    {
        private const string DBMSNotSupported = "Connection to this DBMS is not supported.";

        public static DBMS GetDatabaseServerType(this WrappedConnection wrappedConnection)
        {
            string dbVersion = null;

            try
            {
                dbVersion = QueryForString(wrappedConnection, "SHOW VARIABLES LIKE '%version%';");
                if (!dbVersion.IsNullOrWhiteSpace())
                {
                    return DBMS.MySQL;
                }
            }
            catch { }

            try
            {
                dbVersion = QueryForString(wrappedConnection, "SELECT version()"); // attention ca marche aussi pour mysql
                if (!dbVersion.IsNullOrWhiteSpace()) 
                {
                    return dbVersion.Contains("CockroachDB") ? DBMS.CockroachDb : DBMS.PostgreSQL;
                }
            }
            catch { }

            try
            {
                dbVersion = QueryForString(wrappedConnection, "SELECT @@version");
                if (!dbVersion.IsNullOrWhiteSpace())
                {
                    return DBMS.SQLServer;
                }
            }
            catch { }

            try
            {
                dbVersion = QueryForString(wrappedConnection, "SELECT sqlite_version()");
                if (!dbVersion.IsNullOrWhiteSpace())
                {
                    return DBMS.SQLite;
                }
            }
            catch { }

            try
            {
                dbVersion = QueryForString(wrappedConnection, "select release_version from system.local");
                if (!dbVersion.IsNullOrWhiteSpace())
                    return DBMS.Cassandra;
            }
            catch { }

            throw new EvolveException(DBMSNotSupported);
        }

        public static bool TryBeginTransaction(this WrappedConnection wrappedConnection)
        {
            if (wrappedConnection.CurrentTx == null)
            {
                wrappedConnection.BeginTransaction();
                return true;
            }
            return false;
        }

        public static bool TryCommit(this WrappedConnection wrappedConnection)
        {
            if (wrappedConnection.CurrentTx != null)
            {
                wrappedConnection.Commit();
                return true;
            }
            return false;
        }

        public static bool TryRollback(this WrappedConnection wrappedConnection)
        {
            if (wrappedConnection.CurrentTx != null)
            {
                wrappedConnection.Rollback();
                return true;
            }
            return false;
        }

        public static long QueryForLong(this WrappedConnection wrappedConnection, string sql)
        {
            return Execute(wrappedConnection, sql, cmd =>
            {
                return Convert.ToInt64(cmd.ExecuteScalar());
            });
        }

        public static string QueryForString(this WrappedConnection wrappedConnection, string sql)
        {
            return Execute(wrappedConnection, sql, cmd =>
            {
                return (string)cmd.ExecuteScalar();
            });
        }

        public static bool QueryForBool(this WrappedConnection wrappedConnection, string sql)
        {
            return Execute(wrappedConnection, sql, cmd =>
            {
                return (bool)cmd.ExecuteScalar();
            });
        }

        public static T Query<T>(this WrappedConnection wrappedConnection, string sql)
        {
            return Execute(wrappedConnection, sql, cmd =>
            {
                return (T)cmd.ExecuteScalar();
            });
        }

        public static IEnumerable<string> QueryForListOfString(this WrappedConnection wrappedConnection, string sql)
        {
            return Execute(wrappedConnection, sql, cmd =>
            {
                var list = new List<string>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(reader[0] is DBNull ? null : reader[0].ToString());
                    }
                }

                return list;
            });
        }

        public static IEnumerable<T> QueryForList<T>(this WrappedConnection wrappedConnection, string sql, Func<IDataReader, T> map)
        {
            return Execute(wrappedConnection, sql, cmd =>
            {
                var list = new List<T>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(map(reader));
                    }
                }

                return list;
            });
        }

        public static int ExecuteNonQuery(this WrappedConnection wrappedConnection, string sql, int? commandTimeout = null)
        {
            return Execute(wrappedConnection, sql, cmd =>
            {
                if (commandTimeout != null)
                {
                    cmd.CommandTimeout = commandTimeout.Value;
                }
                return cmd.ExecuteNonQuery();
            });
        }

        public static T ExecuteDbCommand<T>(this WrappedConnection wrappedConnection, string sql, Action<IDbCommand> setupDbCommand, Func<IDbCommand, T> query)
        {
            Check.NotNull(wrappedConnection, nameof(wrappedConnection));
            Check.NotNullOrEmpty(sql, nameof(sql));
            Check.NotNull(query, nameof(query));
            Check.NotNull(setupDbCommand, nameof(setupDbCommand));

            return Execute(wrappedConnection, sql, query, setupDbCommand);
        }

        private static T Execute<T>(WrappedConnection wrappedConnection, string sql, Func<IDbCommand, T> query, Action<IDbCommand> setupDbCommand = null)
        {
            Check.NotNull(wrappedConnection, nameof(wrappedConnection));
            Check.NotNullOrEmpty(sql, nameof(sql));
            Check.NotNull(query, nameof(query));

            bool wasClosed = wrappedConnection.DbConnection.State == ConnectionState.Closed;

            try
            {
                if (wasClosed)
                {
                    wrappedConnection.Open();
                }

                using (IDbCommand cmd = wrappedConnection.DbConnection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Transaction = wrappedConnection.CurrentTx;
                    setupDbCommand?.Invoke(cmd);

                    return query(cmd);
                }
            }
            catch (Exception ex)
            {
                throw new EvolveSqlException(sql, ex);
            }
            finally
            {
                if (wasClosed)
                {
                    wrappedConnection.Close();
                }
            }
        }
    }
}
