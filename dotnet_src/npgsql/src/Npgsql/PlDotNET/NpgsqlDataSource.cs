using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Npgsql.Internal;
using Npgsql.PostgresTypes;
using PlDotNET.Handler;
using PlDotNET.Common;

namespace Npgsql;

/// <summary>
/// Represents a modified version of the NpgsqlDataSource class that inherits from the original NpgsqlMultiHostDataSource class.
/// This functions doesn't inherit from the original NpgsqlDataSource class because it is an abstract class (<see cref="NpgsqlDataSourceOrig" />).
/// </summary>
public class NpgsqlDataSource : NpgsqlMultiHostDataSourceOrig
{
    /// <summary>
    /// Internal constructor
    /// </summary>
    internal NpgsqlDataSource() : base() {}

    internal NpgsqlDataSource(
        NpgsqlConnectionStringBuilder settings,
        NpgsqlDataSourceConfiguration dataSourceConfig) : this()
    {
        Settings = settings;
        Configuration = dataSourceConfig;
    }

    /// <summary>
    /// Creates a new <see cref="NpgsqlDataSource" />
    /// </summary>
    public static new NpgsqlDataSource Create(string connectionString = "")
    {
        return new NpgsqlDataSource();
    }

    /// <summary>
    /// Returns a <see cref="NpgsqlConnection" /> with a NpgsqlDataSource already set
    /// </summary>
    public new NpgsqlConnection CreateConnection()
        => NpgsqlConnection.FromDataSource(this);


    /// <summary>
    /// Returns a <see cref="NpgsqlCommand" /> with the provided query
    /// </summary>
    public new NpgsqlCommand CreateCommand(string query)
    {
        return new NpgsqlCommand(query);
    }

    /// <summary>
    /// Returns a <see cref="NpgsqlConnection" /> as a ValueTask with the connection open
    /// </summary>
    public new ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var result = OpenConnection();
        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Returns a <see cref="NpgsqlConnection" /> with the connection open
    /// </summary>
    public new NpgsqlConnection OpenConnection()
    {
        var connection = this.CreateConnection();

        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore()
    {
        Elog.Info($"Calling NpgsqlDataSource.DisposeAsyncCore");
        return ValueTask.CompletedTask;
    }
}
