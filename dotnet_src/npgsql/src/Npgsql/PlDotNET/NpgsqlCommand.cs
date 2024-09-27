using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Npgsql.Internal;
using Npgsql.PostgresTypes;
using PlDotNET.Handler;
using PlDotNET.Common;

#pragma warning disable CS8604

namespace Npgsql;

/// <summary>
/// Represents a modified version of the NpgsqlCommand class that inherits from the
/// original Npgsql class (<see cref="NpgsqlCommandOrig"/>).
/// </summary>
public class NpgsqlCommand : NpgsqlCommandOrig
{
    internal IntPtr _cmdPointer = IntPtr.Zero;

    /// <summary>
    /// Constructor
    /// </summary>
    public NpgsqlCommand() : this(null, null, null) { }

    /// <summary>
    /// Constructor which a CommandText is set
    /// </summary>
    public NpgsqlCommand(string? cmdText) : this(cmdText, null, null) { }

    /// <summary>
    /// Returns a <see cref="NpgsqlCommand" /> with a NpgsqlConnection already set
    /// in the InternalConnection attribute and CommandText too
    /// </summary>
    public NpgsqlCommand(string? cmdText, NpgsqlConnection? connection) : base(cmdText, connection)
    {
        // Create an internal connection if necessary
        if (connection == null)
        {
            InternalConnection = new NpgsqlConnection();
            InternalConnection.Open();
        }
    }

    /// <summary>
    /// Returns a <see cref="NpgsqlCommand" /> with a NpgsqlTransaction already set
    /// in the Transaction attribute, a NpgsqlConnection already set
    /// in the InternalConnection attribute and CommandText too
    /// </summary>
    public NpgsqlCommand(string? cmdText, NpgsqlConnection? connection, NpgsqlTransaction? transaction)
        : this(cmdText, connection)
        => Transaction = transaction;


    internal NpgsqlCommand(int batchCommandCapacity, NpgsqlConnection? connection = null) : base(batchCommandCapacity, connection)
    {
        _parameters = new NpgsqlParameterCollection();
    }

    internal NpgsqlCommand(string? cmdText, NpgsqlConnector connector) : this(cmdText)
        => _connector = connector;

    internal NpgsqlCommand(NpgsqlConnector connector, int batchCommandCapacity) : this(batchCommandCapacity)
        => _connector = connector;

    /// <inheritdoc />
    [AllowNull, DefaultValue("")]
    [Category("Data")]
    public override string CommandText
    {
        get => _commandText;
        set
        {
            if (value == null || value == string.Empty)
            {
                throw new Exception("Null command error!");
            }
            _commandText = value;
        }
    }

    /// <inheritdoc />
    internal override Task Prepare(bool async, CancellationToken cancellationToken = default)
    {
        // Code from original version
        var connection = CheckAndGetConnection();
        Debug.Assert(connection is not null);
        if (connection.Settings.Multiplexing)
            throw new NotSupportedException("Explicit preparation not supported with multiplexing");

        // TODO: Evaluate what exactly it is necessary to do here
        // The original code calls ProcessRawQuery what we are doing in ExecuteReader

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    internal async override Task Unprepare(bool async, CancellationToken cancellationToken = default)
    {
        // Code from original version
        var connection = CheckAndGetConnection();
        Debug.Assert(connection is not null);
        if (connection.Settings.Multiplexing)
            throw new NotSupportedException("Explicit preparation not supported with multiplexing");
        // TODO: Evaluate what exactly it is necessary to do here
        // The original code loop over the InternalBatchCommands modifying them

        // Fake the async behavior
        await Task.Run(() => { }, cancellationToken);

        return;
    }

    /// <inheritdoc />
    internal override async ValueTask<NpgsqlDataReader> ExecuteReader(CommandBehavior behavior, bool async, CancellationToken cancellationToken)
    {
        async = false;

        // Performs some checks made on the original code
        var conn = CheckAndGetConnection();
        Debug.Assert(conn is not null);

        NpgsqlConnector? connector;
        if (_connector is not null)
        {
            Debug.Assert(conn is null);
            if (behavior.HasFlag(CommandBehavior.CloseConnection))
                throw new ArgumentException($"{nameof(CommandBehavior.CloseConnection)} is not supported with {nameof(NpgsqlConnector)}", nameof(behavior));
            connector = _connector;
        }
        else
        {
            Debug.Assert(conn is not null);
            conn.TryGetBoundConnector(out connector);
        }

        if (connector is null)
        {
            connector = new NpgsqlConnector();
            conn!.Connector = connector;
            connector.Connection = conn;
        }

        Func<Task<NpgsqlDataReader>> executeAsync = async () =>
        {

            // Currently, pldotnet operates using synchronous execution.
            async = false;

            IntPtr errorDataPtr = IntPtr.Zero;

            State = CommandState.InProgress;

            var parser = new SqlQueryParser();

            if (IsWrappedByBatch)
            {
                foreach (var batchCommand in InternalBatchCommands)
                {
                    parser.ParseRawQuery(batchCommand, true, true);
                }
            }
            else
            {
                parser.ParseRawQuery(this, true, true);
            }

            List<IntPtr> tupleTables = new List<IntPtr>();
            List<ulong> processedRows = new List<ulong>();

            // PositionalParameters are not sent to the BatchCommand so we need to take the parameters from the NpgsqlCommand
            bool HasPositionalParameters = this.Parameters.Count > 0 && this.Parameters[0].IsPositional;

            foreach (NpgsqlParameter parameter in this.Parameters.InternalList)
            {
                if (parameter.IsPositional != HasPositionalParameters)
                {
                    throw new NotSupportedException("Mixing named and positional parameters isn't supported");
                }
            }

            foreach (NpgsqlBatchCommand batchCommand in this.InternalBatchCommands)
            {
                var query = batchCommand.FinalCommandText;

                var parameters = new List<NpgsqlParameter>();

                // Get the right parameters list
                if (IsWrappedByBatch)
                {
                    parameters = batchCommand.PositionalParameters.Count == 0 ? batchCommand.Parameters.InternalList : batchCommand.PositionalParameters;
                }
                else
                {
                    parameters = batchCommand.PositionalParameters.Count == 0 ? this.Parameters.InternalList : batchCommand.PositionalParameters;
                }

                if (parameters.Count > 0)
                {
                    // Prepare arrays to send to PostgreSQL
                    uint[] paramTypesOid = new uint[parameters.Count];
                    IntPtr[] paramValues = new IntPtr[parameters.Count];
                    char[] nullmap = new char[parameters.Count];
                    for (int i = 0; i < parameters.Count; i++)
                    {
                        paramTypesOid[i] = NpgsqlHelper.FindOid(parameters[i].NpgsqlDbType);
                        paramValues[i] = DatumConversion.OutputNullableValue((OID)paramTypesOid[i], parameters[i].Value);
                        nullmap[i] = parameters[i].Value == null || DBNull.Value.Equals(parameters[i].Value) ? 'n' : ' ';
                    }

                    // TODO: refactor pldotnet_SPIPrepare to return the PlanPtr
                    // TODO: rename _cmdPointer -> let's use something related to SPIPlanPtr
                    SPI.pldotnet_SPIPrepare(ref this._cmdPointer, query, parameters.Count, paramTypesOid, ref errorDataPtr);
                    if (errorDataPtr != IntPtr.Zero)
                    {
                        Elog.Warning("The code fails in C!");
                        SPIHelper.HandlePostgresqlError(errorDataPtr);
                    }

                    // TODO: investigate how Npgsql controls readonly queries (it is false!)
                    // TODO: also investigate how Npgsql controls the limit of rows (it is 0 the same default value of plpython)
                    tupleTables.Add(SPI.pldotnet_SPIExecutePlan(this._cmdPointer, paramValues, nullmap, false, 0, ref errorDataPtr));
                    if (errorDataPtr != IntPtr.Zero)
                    {
                        Elog.Warning("The code fails in C!");
                        SPIHelper.HandlePostgresqlError(errorDataPtr);
                    }
                }
                else
                {
                    // The query doesn't have parameters
                    // TODO: investigate how Npgsql controls readonly queries (it is false!)
                    // TODO: also investigate how Npgsql controls the limit of rows (it is 0 the same default value of plpython)
                    tupleTables.Add(SPI.pldotnet_SPIExecute(query, false, 0, ref errorDataPtr));
                    if (errorDataPtr != IntPtr.Zero)
                    {
                        Elog.Warning("The code fails in C!");
                        SPIHelper.HandlePostgresqlError(errorDataPtr);
                    }
                }
                processedRows.Add(SPI.pldotnet_GetProcessedRowsNumber());
            }

            var reader = new NpgsqlDataReader(connector)
            {
                SPITupleTables = tupleTables,
                ProcessedRows = processedRows,
            };
            // TODO: the original code passes the BatchCommands to the reader and run them there, we should do the same
            reader.Init(this, behavior, InternalBatchCommands);
            connector.DataReader = reader;

            if (async)
                await reader.NextResultAsync(cancellationToken);
            else
                reader.NextResult();

            return reader;
        };

        try
        {
            if (async)
            {
                return await executeAsync();
            }
            else
            {
                return executeAsync().GetAwaiter().GetResult();
            }
        }
        catch (Exception e)
        {
            // If fails, do the same as the original code
            var reader = connector?.CurrentReader;
            if (e is not NpgsqlOperationInProgressException && reader is not null)
                await reader.Cleanup(async);

            State = CommandState.Idle;

            if ((behavior & CommandBehavior.CloseConnection) == CommandBehavior.CloseConnection)
            {
                Debug.Assert(_connector is null && conn is not null);
                conn.Close();
            }

            throw;
        }
    }

    /// <inheritdoc />
    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    {
        var result = ExecuteScalar();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public override object? ExecuteScalar()
    {
        var reader = ExecuteReader();
        try
        {
            var read = reader.Read();

            var value = read && reader.FieldCount != 0 ? reader.GetValue(0) : null;

            while (reader.NextResult()) ;

            return value;
        }
        finally
        {
            reader.Dispose();
        }
    }

    /// <inheritdoc />
    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        int result = ExecuteNonQuery();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public override int ExecuteNonQuery()
    {
        var reader = ExecuteReader();
        try
        {
            while (reader.NextResult()) ;

            return reader.RecordsAffected;
        }
        finally
        {
            reader.Dispose();
        }
    }
}
