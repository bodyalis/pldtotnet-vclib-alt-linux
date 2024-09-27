using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql.Internal;
using PlDotNET.Handler;
using PlDotNET.Common;

namespace Npgsql;

#pragma warning disable CS8600

/// <summary>
/// Represents a transaction to be made in a PostgreSQL database. This class cannot be inherited.
/// </summary>
public class NpgsqlTransaction : NpgsqlTransactionOrig
{
    internal NpgsqlTransaction(NpgsqlConnector connector = default!) : base() { }


    internal Dictionary<IsolationLevel, string> IsolationLevels = new Dictionary<IsolationLevel, string>
    {
        { IsolationLevel.RepeatableRead, "REPEATABLE READ" },
        { IsolationLevel.Snapshot, "REPEATABLE READ" },
        { IsolationLevel.Serializable, "SERIALIZABLE" },
        { IsolationLevel.ReadUncommitted, "READ UNCOMMITTED" },
        { IsolationLevel.ReadCommitted, "READ COMMITTED" },
    };

    internal new void Init(IsolationLevel isolationLevel = DefaultIsolationLevel)
    {
        Debug.Assert(isolationLevel != IsolationLevel.Chaos);

        if (isolationLevel == IsolationLevel.Unspecified)
        {
            isolationLevel = DefaultIsolationLevel;
        }

        if (!IsolationLevels.TryGetValue(isolationLevel, out string level))
        {
            throw new NotSupportedException("Isolation level not supported: " + isolationLevel);
        }

        // TODO: we need to find a way to create a valid NpgsqlConnector to update it according to pldotnet moves
        // _connector.TransactionStatus = TransactionStatus.Pending;
        _isolationLevel = isolationLevel;
        IsDisposed = false;

        SPI.Execute($"SET TRANSACTION ISOLATION LEVEL {level}");
    }

    /// <inheritdoc />
    public override Task CommitAsync(CancellationToken cancellationToken = default)
    {
        Commit();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Commit()
    {
        // https://www.postgresql.org/docs/current/spi-spi-commit.html
        IntPtr errorDataPtr = IntPtr.Zero;
        SPI.pldotnet_SPICommit(ref errorDataPtr);
        if (errorDataPtr != IntPtr.Zero)
        {
            Elog.Warning("The code fails in C!");
            SPIHelper.HandlePostgresqlError(errorDataPtr);
        }
    }

    /// <inheritdoc />
    public override Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        Rollback();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Rollback()
    {
        // https://www.postgresql.org/docs/current/spi-spi-rollback.html
        IntPtr errorDataPtr = IntPtr.Zero;
        SPI.pldotnet_SPIRollback(ref errorDataPtr);
        if (errorDataPtr != IntPtr.Zero)
        {
            Elog.Warning("The code fails in C!");
            SPIHelper.HandlePostgresqlError(errorDataPtr);
        }
    }

    /// <inheritdoc />
    public override Task RollbackAsync(string name, CancellationToken cancellationToken = default)
    {
        Rollback(name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Rollback(string name)
    {
        var quotedName = RequiresQuoting(name) ? $"\"{name.Replace("\"", "\"\"")}\"" : name;

        // https://www.postgresql.org/docs/current/sql-rollback-to.html
        SPI.Execute($"ROLLBACK TO SAVEPOINT {quotedName}");
    }

    /// <inheritdoc />
    public override Task SaveAsync(string name, CancellationToken cancellationToken = default)
    {
        Save(name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Save(string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name can't be empty", nameof(name));

        LogMessages.CreatingSavepoint(_transactionLogger, name, _connector.Id);

        if (RequiresQuoting(name))
            name = $"\"{name.Replace("\"", "\"\"")}\"";

        // https://www.postgresql.org/docs/current/sql-savepoint.html
        SPI.Execute("SAVEPOINT {name}");
    }

    /// <inheritdoc />
    public override Task ReleaseAsync(string name, CancellationToken cancellationToken = default)
    {
        Release(name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Release(string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name can't be empty", nameof(name));

        var quotedName = RequiresQuoting(name) ? $"\"{name.Replace("\"", "\"\"")}\"" : name;

        // https://www.postgresql.org/docs/current/sql-release-savepoint.html
        SPI.Execute($"RELEASE SAVEPOINT {quotedName}");
    }
}
