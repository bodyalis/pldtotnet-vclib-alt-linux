using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Npgsql.Internal;
using Npgsql.PostgresTypes;
using PlDotNET.Handler;
using PlDotNET.Common;

namespace Npgsql;

/// <summary>
/// Represents a modified version of the NpgsqlConnection class that inherits from the
/// original Npgsql class (<see cref="NpgsqlConnectionOrig"/>).
/// </summary>
[System.ComponentModel.DesignerCategory("")]
public class NpgsqlConnection : NpgsqlConnectionOrig
{
    NpgsqlDataSource? _dataSource;

    /// <summary>
    /// Constructor
    /// </summary>
    public NpgsqlConnection() : base() { }

    /// <summary>
    /// Constructor which a ConnectionString is set
    /// </summary>
    public NpgsqlConnection(string? connectionString) : base(connectionString) { }

    internal NpgsqlConnection(NpgsqlDataSource dataSource, NpgsqlConnector connector) : base(dataSource, connector) { }

    /// <summary>
    /// Returns a <see cref="NpgsqlConnection" /> with a NpgsqlDataSource already set
    /// </summary>
    internal new static NpgsqlConnection FromDataSource(NpgsqlDataSource dataSource)
    {
        Elog.Info("Created connection with FromDataSource function");
        var conn = new NpgsqlConnection();
        conn._dataSource = dataSource;
        return conn;
    }

    /// <inheritdoc/>
    internal override Task Open(bool async, CancellationToken cancellationToken)
    {
        // Currently, pldotnet operates using synchronous execution.
        async = false;

        CheckClosed();
        Debug.Assert(Connector == null);

        FullState = ConnectionState.Connecting;
        // Since we are already connected to the database, nothing to do
        FullState = ConnectionState.Open;

        // Set Connector
        ConnectorBindingScope = ConnectorBindingScope.Connection;
        Connector = new NpgsqlConnector();
        Connector.Connection = this;
        Connector.State = ConnectorState.Ready;

        this._dataSource = NpgsqlDataSource.Create();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    internal override Task Close(bool async)
    {
        // TODO: the original version has the below assertions, but we are not prepared to handle them
        // Debug.Assert(Connector != null);
        // Debug.Assert(ConnectorBindingScope != ConnectorBindingScope.None);

        switch (FullState)
        {
            case ConnectionState.Open:
            case ConnectionState.Open | ConnectionState.Executing:
            case ConnectionState.Open | ConnectionState.Fetching:
                break;
            case ConnectionState.Broken:
                FullState = ConnectionState.Closed;
                goto case ConnectionState.Closed;
            case ConnectionState.Closed:
                return Task.CompletedTask;
            case ConnectionState.Connecting:
                throw new InvalidOperationException("Can't close, connection is in state " + FullState);
            default:
                throw new ArgumentOutOfRangeException("Unknown connection state: " + FullState);
        }

        FullState = ConnectionState.Closed;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the NpgsqlDataSource value
    /// </summary>
    internal new NpgsqlDataSource NpgsqlDataSource
    {
        get
        {
            if (_dataSource == null)
            {
                _dataSource = NpgsqlDataSource.Create();
            }
            return _dataSource;
        }
    }

    /// <inheritdoc/>
    public override NpgsqlCommand CreateCommand()
    {
        return new NpgsqlCommand(null, this) { IsCached = true };
    }

    /// <inheritdoc/>
    public override void UnprepareAll()
    {
        CheckReady();

        // TODO: the original code calls the below code; need to ensure the same behavior
        // using (Connector!.StartUserAction())
        //     Connector.UnprepareAll();
    }

    /// <summary>
    /// Returns the PostgreSqlVersion as a <see cref="Version" /> object.
    /// </summary>
    public new Version PostgreSqlVersion
    {
        get
        {
            IntPtr versionPtrStr = SPI.pldotnet_GetPostgreSqlVersion();
            return new Version(Marshal.PtrToStringAuto(versionPtrStr) ?? string.Empty);
        }
    }

    /// <inheritdoc/>
    internal override async ValueTask<NpgsqlTransaction> BeginTransaction(IsolationLevel level, bool async, CancellationToken cancellationToken)
    {
        // Currently, pldotnet operates using synchronous execution.
        async = false;

        if (level == IsolationLevel.Chaos)
            throw new NotSupportedException("Unsupported IsolationLevel: " + level);

        var transaction = new NpgsqlTransaction(default!);
        transaction.Init(level);

        // Important: To ensure the same behavior as Npgsql, it's crucial to call `SPI_commit()` at this point.
        // Otherwise, invoking `SPI_rollback()` would revert all changes made prior to beginning the Npgsql transaction.
        // This occurs because the PostgreSQL transaction initiates alongside the user procedure.
        if (async)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            transaction.Commit();
        }

        return transaction;
    }
}
