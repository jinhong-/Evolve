﻿using System;
using System.Collections.Generic;
using Evolve.Connection;
using Evolve.Dialect.Cassandra;
using Evolve.Dialect.MySQL;
using Evolve.Dialect.PostgreSQL;
using Evolve.Dialect.SQLite;
using Evolve.Dialect.SQLServer;
using Evolve.Utilities;

namespace Evolve.Dialect
{
    public class DatabaseHelperFactory : IDatabaseHelperFactory
    {
        private const string UnknownDBMS = "Unknown DBMS {0}.";

        private static readonly Dictionary<DBMS, Func<WrappedConnection, DatabaseHelper>> _dbmsMap = new Dictionary<DBMS, Func<WrappedConnection, DatabaseHelper>>
        {
            [DBMS.SQLite]           = wcnn => new SQLiteDatabase(wcnn),
            [DBMS.PostgreSQL]       = wcnn => new PostgreSQLDatabase(wcnn),
            [DBMS.MySQL]            = wcnn => new MySQLDatabase(wcnn),
            [DBMS.MariaDB]          = wcnn => new MySQLDatabase(wcnn),
            [DBMS.SQLServer]        = wcnn => new SQLServerDatabase(wcnn),
            [DBMS.Cassandra]        = wcnn => new CassandraCluster(wcnn),
        };

        public DatabaseHelper GetDatabaseHelper(DBMS dbmsType, WrappedConnection connection)
        {
            Check.NotNull(connection, nameof(connection));

            _dbmsMap.TryGetValue(dbmsType, out Func<WrappedConnection, DatabaseHelper> dbHelperCreationDelegate);
            if(dbHelperCreationDelegate == null)
            {
                throw new EvolveException(string.Format(UnknownDBMS, dbmsType));
            }

            return dbHelperCreationDelegate(connection);
        }
    }
}
