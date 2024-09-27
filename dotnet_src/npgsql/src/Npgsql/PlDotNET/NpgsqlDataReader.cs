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
using PlDotNET.Common;
using PlDotNET.Handler;
using Npgsql.TypeMapping;

using Npgsql.Internal.TypeHandlers;
using Npgsql.Internal.TypeHandling;

using Npgsql.BackendMessages;

#pragma warning disable CS8618, CS8619, CS8604, CS8600, CS8603

namespace Npgsql;

/// <summary>
/// Represents a modified version of the NpgsqlDataReader class that inherits from the original NpgsqlDataReader class
/// (<see cref="NpgsqlDataReaderOrig"/>) provided by Npgsql to work with pldotnet procedural language.
/// </summary>
public class NpgsqlDataReader : NpgsqlDataReaderOrig
{
    /// <summary>
    /// The current row of the result set
    /// </summary>
    private ulong? CurrentRowIndex = null;

    /// <summary>
    /// Identify which query result is being read.
    /// </summary>
    private int ResultIndex = -1;

    /// <summary>
    /// Store the SPI_tuptables related to the processed queries.
    /// It will be a null pointer if the query doesn't returned a table.
    /// </summary>
    internal List<IntPtr> SPITupleTables;

    /// <summary>
    /// Store the number of processed rows for each executed query.
    /// </summary>
    internal List<ulong> ProcessedRows;

    /// <summary>
    /// The current row of the result set, where each item is a PostgreSQL datum.
    /// </summary>
    private IntPtr[] CurrentRow;

    /// <summary>
    /// The nullmap of the current row.
    /// </summary>
    private byte[] IsNull;

    /// <summary>
    /// If byOID is up to date
    /// </summary>
    private static bool isByOIDUpdated = false;

    /// <summary>
    /// Type mapper (OID to PostgresType)
    /// </summary>
    private static Dictionary<uint, PostgresType> byOID = new Dictionary<uint, PostgresType>();

    /// <summary>
    /// Internal constructor
    /// </summary>
    internal NpgsqlDataReader() : base() { }

    /// <summary>
    /// Constructor
    /// </summary>
    internal NpgsqlDataReader(NpgsqlConnector connector) : base(connector) { }

    /// <summary>
    /// Retrieve information of a typemapper used to get Postgres Type names
    /// </summary>
    internal void FetchTypeMapper()
    {

        // The following query will return a table with these columns:
        // | nspname      | oid | typname      | typtype | typnotnull | elemtypoid |
        // |--------------|-----|--------------|---------|------------|------------|
        // | pg_catalog   | 21  | int2         | b       | f          |            |
        // | ...          | ... | ...          | ...     | ...        | ...        |

        var loadTypesQuery = PostgresDatabaseInfo.GenerateLoadTypesQuery(false, false, false);
        IntPtr typesPtr;

        IntPtr errorDataPtr = IntPtr.Zero;
        typesPtr = SPI.pldotnet_SPIExecute(loadTypesQuery, true, 0, ref errorDataPtr);

        int ntypes = SPI.pldotnet_GetTableColumnNumber(typesPtr);

        IntPtr[] tmpResultTypes = new IntPtr[ntypes];
        var nullmap = new byte[ntypes];

        int rowcount = (int)SPI.pldotnet_GetProcessedRowsNumber();
        Elog.Info($"Total types detected in DB: {rowcount}");

        byOID = new Dictionary<uint, PostgresType>();

        char typtype;
        for (int j = 0; j < rowcount; j++)
        {
            SPI.pldotnet_GetRow(j, tmpResultTypes, nullmap, typesPtr);

            var pgtnamespace = Marshal.PtrToStringAuto(tmpResultTypes[0]); // namespace
            var pgtname = Marshal.PtrToStringAuto(tmpResultTypes[2]); // name
            var pgtoid = (uint)tmpResultTypes[1]; // oid
            var elemtypoid = tmpResultTypes[5];

            typtype = (char)(uint)tmpResultTypes[3]; //typtype

            // TODO: Add support for other types when PlDotNET supports them
            // Check the default implementation of Npgsql (see PostgresDatabaseInfo.cs)
            switch (typtype)
            {
                case 'b': // Normal base type
                    byOID[pgtoid] = new PostgresBaseType(
                        pgtnamespace,
                        pgtname,
                        pgtoid
                    );
                    Elog.Info($"{pgtoid} ====> {pgtname}");
                    continue;

                case 'a': // Array
                    {
                        if (!byOID.TryGetValue((uint)elemtypoid, out var elementPostgresType))
                        {
                            Elog.Info($"Array type '{pgtname}' refers to unknown element with OID {elemtypoid}, skipping");
                            continue;
                        }

                        var arrayType = new PostgresArrayType(
                            pgtnamespace,
                            pgtname,
                            pgtoid,
                            elementPostgresType);

                        byOID[arrayType.OID] = arrayType;
                        continue;
                    }

                case 'p': // pseudo-type (record, void)
                    goto case 'b'; // Hack this as a base type

                case 'r': // Range
                    continue;

                case 'm': // Multirange
                    continue;

                case 'e': // Enum
                    continue;

                case 'c': // Composite
                    continue;

                case 'd': // Domain
                    continue;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown typtype for type '{pgtname}' in pg_type: {typtype}");
            }
        }

        isByOIDUpdated = true;
    }

    /// <summary>
    /// Retrieve information of table.
    /// </summary>
    internal void FetchTableInformation()
    {
        Connector.State = ConnectorState.Fetching;
        int NCols = SPI.pldotnet_GetTableColumnNumber(this.SPITupleTables[ResultIndex]);

        OID[] ColumnTypes;
        string[] ColumnNames;
        uint[] columnTypmods;
        short[] columnLens;

        if (NCols > 0)
        {
            this.CurrentRow = new IntPtr[NCols];
            this.IsNull = new byte[NCols];

            IntPtr[] columnNamePts = new IntPtr[NCols];
            int[] oidTypes = new int[NCols];
            int[] Typmods = new int[NCols];
            int[] Lens = new int[NCols];

            SPI.pldotnet_GetColProps(oidTypes, columnNamePts, Typmods, Lens, this.SPITupleTables[ResultIndex]);

            ColumnTypes = oidTypes.Select(i => (OID)i).ToArray();
            ColumnNames = columnNamePts.ToList().Select(namePts => Marshal.PtrToStringAuto(namePts)).ToArray();
            columnTypmods = Typmods.Select(i => (uint)i).ToArray();
            columnLens = Lens.Select(i => (short)i).ToArray();

            this.CurrentRowIndex = 0;

            this._recordsAffected = this.ProcessedRows[ResultIndex] == 0 ? null : (ulong)this.ProcessedRows[ResultIndex];

            RowDescriptionMessage rd = new(Math.Max(NCols, 1));
            for (int i = 0; i < NCols; i++)
            {
                if (rd is not null)
                {
                    if (rd._fields is not null)
                    {
                        uint tableoid = (uint)SPI.pldotnet_GetTableTypeID(this.SPITupleTables[ResultIndex]);

                        rd._fields[i] = new FieldDescription(
                            name: ColumnNames[i],
                            tableOID: tableoid,
                            columnAttributeNumber: checked((short)i),
                            oid: (uint)ColumnTypes[i],
                            typeSize: (short)columnLens[i],
                            typeModifier: (int)columnTypmods[i],

                            // See pldotnet/dotnet_src/npgsql/src/Npgsql/Util/PGUtil.cs
                            // formatCode is 0 for text mode
                            formatCode: 0
                        );

                        if (rd is not null) rd._nameIndex.TryAdd(ColumnNames[i], i);

                    }
                    else
                    {
                        Elog.Warning($"RowDescription _fields is null!");
                    }
                }
                else
                {
                    Elog.Warning($"RowDescription is null!");
                }

            }
            RowDescription = rd;
            if (rd != null) rd.Count = NCols;
        }
    }

    /// <inheritdoc/>
    public override Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        var result = Read();
        return Task.FromResult(result);
    }

    /// <summary>
    /// Checks that we have a RowDescription, but not necessary an actual resultset
    /// (for operations which work in SchemaOnly mode.
    /// </summary>
    public override Type GetFieldType(int ordinal)
    {
        FieldDescription? f = RowDescription?._fields[ordinal];
        if (f != null)
        {
            OID t = (OID)f.TypeOID;
            return DatumConversion.GetFieldType(t);
        }
        return GetField(ordinal).FieldType;
    }

    /// <summary>
    /// Advances the reader to the next row in a result set.
    /// </summary>
    /// <returns><b>true</b> if there are more rows; otherwise <b>false</b>.</returns>
    public override bool Read()
    {
        CheckClosedOrDisposed();

        try
        {
            switch (State)
            {
                case ReaderState.BeforeResult:
                    // First Read() after NextResult. Data row has already been processed.
                    State = ReaderState.InResult;
                    return true;

                case ReaderState.InResult:
                    break;

                case ReaderState.BetweenResults:
                case ReaderState.Consumed:
                case ReaderState.Closed:
                case ReaderState.Disposed:
                    return false;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Check if there are more results to read
            if (this.ResultIndex >= SPITupleTables.Count)
            {
                // No more query results to read
                return false;
            }

            // Check if there are more rows in the current result
            if (this.CurrentRowIndex < this.ProcessedRows[ResultIndex])
            {
                SPI.pldotnet_GetRow((int)this.CurrentRowIndex, this.CurrentRow, this.IsNull, SPITupleTables[ResultIndex]);
                this.CurrentRowIndex++;

                return true;
            }

            return false;
        }
        catch
        {
            State = ReaderState.Consumed;
            throw;
        }
    }

    /// <summary>
    /// Gets the data type information for the specified field.
    /// This is the PostgreSQL type name (e.g. double precision), not the .NET type
    /// (see <see cref="GetDataTypeName"/> for that).
    /// </summary>
    /// <param name="ordinal">The zero-based column index.</param>
    public override string GetDataTypeName(int ordinal)
    {
        int typemod = RowDescription?._fields?[ordinal]?.TypeModifier ?? -1;
        uint typeoid = RowDescription?._fields?[ordinal]?.TypeOID ?? 0;

        if (!(byOID.ContainsKey(typeoid)) && !isByOIDUpdated)
        {
            Elog.Info("Calling FetchTypeMapper()");
            FetchTypeMapper();
        }
        return byOID[typeoid].GetDisplayNameWithFacets(typemod);
    }

    /// <summary>
    /// Synchronously gets the value of the specified column as a type.
    /// </summary>
    /// <typeparam name="T">Synchronously gets the value of the specified column as a type.</typeparam>
    /// <param name="ordinal">The column to be retrieved.</param>
    /// <returns>The column to be retrieved.</returns>
    public override T GetFieldValue<T>(int ordinal)
    {
        FieldDescription? f = RowDescription?._fields[ordinal];
        // Check if the value of the column is non-null
        if (this.IsNull[ordinal] == 0)
        {
            if (f != null)
            {
                bool arrayAllowsNullElements = false;
                if (typeof(T).IsArray)
                {
                    Type elementType = typeof(T).GetElementType();
                    if (elementType != null) // Check if elementType is not null
                    {
                        arrayAllowsNullElements = !elementType.IsValueType || Nullable.GetUnderlyingType(elementType) != null;
                    }
                }

                OID t = (OID)f.TypeOID;
                object value = DatumConversion.InputValue(this.CurrentRow[ordinal], t, arrayAllowsNullElements);

                Type targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                if (targetType != typeof(T))
                {
                    // If T is nullable and targetType is the underlying type
                    object convertedValue = Convert.ChangeType(value, targetType);
                    return (T)Activator.CreateInstance(typeof(T), convertedValue);
                }
                else
                {
                    // If T is not nullable
                    return (T)Convert.ChangeType(value, typeof(T));
                }
            }
            else
            {
                throw new System.Exception($"FieldDescription is null for Column {ordinal} and nullmap is not null.");
            }
        }

        // If the value is null and T is "object", Npgsql returns (T)(object)DBNull.Value
        if (typeof(T) == typeof(object))
        {
            return (T)(object)DBNull.Value;
        }

        // It will be true if T is nullable or a reference type, false otherwise
        if (Nullable.GetUnderlyingType(typeof(T)) != null || !typeof(T).IsValueType)
        {
            if (f != null)
            {
                OID t = (OID)f.TypeOID;
                return (T)(object)DatumConversion.InputNullableValue(this.CurrentRow[ordinal], t, true);
            }
            else
            {
                throw new System.Exception($"FieldDescription is not null for null Column {ordinal} and nullmap is null.");
            }
        }

        throw new InvalidCastException($"Column '{GetName(ordinal)}' is null.");
    }

    /// <summary>
    /// Populates an array of objects with the column values of the current row.
    /// </summary>
    /// <param name="values">An array of Object into which to copy the attribute columns.</param>
    /// <returns>The number of instances of <see cref="object"/> in the array.</returns>
    public override int GetValues(object[] values)
    {
        var count = Math.Min(FieldCount, values.Length);
        for (var i = 0; i < count; i++)
            values[i] = GetValue(i);
        return count;
    }

    /// <summary>
    /// Gets the value of the specified column as an instance of <see cref="object"/>.
    /// </summary>
    /// <param name="ordinal">The zero-based column ordinal.</param>
    /// <returns>The value of the specified column.</returns>
    public override object GetValue(int ordinal)
    {
        // Check if the value of the column is null
        // If so, it should return DBNull.Value in the same way as Npgsql
        if (this.IsNull[ordinal] != 0)
        {
            return DBNull.Value;
        }
        FieldDescription? f = RowDescription?._fields[ordinal];
        if (f != null)
        {
            OID t = (OID)f.TypeOID;
            return DatumConversion.InputValue(this.CurrentRow[ordinal], t);
        }

        return DBNull.Value;
    }

    /// <summary>
    /// Gets the value of the specified column as a <see cref="DateOnly"/> object.
    /// </summary>
    public DateOnly GetDateOnly(int ordinal) => GetFieldValue<DateOnly>(ordinal);

    /// <inheritdoc />
    public override ValueTask DisposeAsync()
    {
        Close(connectionClosing: false, async: false, isDisposing: true).GetAwaiter().GetResult();
        return new ValueTask();
    }

    /// <summary>
    /// Closes the NpgsqlDataReader, finishing the SPI connection.
    /// </summary>
    internal override async Task Close(bool connectionClosing, bool async, bool isDisposing)
    {
        if (State is ReaderState.Closed or ReaderState.Disposed)
        {
            if (isDisposing)
                State = ReaderState.Disposed;
            return;
        }

        State = ReaderState.Closed;
        Command.State = CommandState.Idle;
        Connector.CurrentReader = null;

        // The original method calls Connector.EndUserAction() to update the connector state
        Connector.State = ConnectorState.Ready;

        // Code from the original version
        if (isDisposing)
            State = ReaderState.Disposed;

        if (_connection?.ConnectorBindingScope == ConnectorBindingScope.Reader)
        {
            UnbindIfNecessary();

            _connection.Connector = null;
            Connector.Connection = null;
            _connection.ConnectorBindingScope = ConnectorBindingScope.None;

            if (_behavior.HasFlag(CommandBehavior.CloseConnection) && !connectionClosing)
                _connection.Close();

            Connector.ReaderCompleted.SetResult(null);
        }
        else if (_behavior.HasFlag(CommandBehavior.CloseConnection) && !connectionClosing)
        {
            Debug.Assert(_connection is not null);
            _connection.Close();
        }

        // Fake the async behavior
        await Task.Run(() => { });
    }

    /// <summary>
    /// Calls Read method and return a Task with it's bool response
    /// </summary>
    /// <param name="cancellationToken">
    /// The cancellation token to satisfy the override. It's ignored
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation that returns true if there
    /// are more result sets; otherwise false.
    /// </returns>
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken)
    {
        var result = NextResult();
        return Task.FromResult(result);
    }

    /// <summary>
    /// Calls the Read Function and return it's value
    /// </summary>
    /// <returns>true if there are more result sets; otherwise false.</returns>
    public override bool NextResult()
    {
        this.ResultIndex++;
        if (this.ResultIndex < SPITupleTables.Count)
        {
            State = ReaderState.BeforeResult;
            this.CurrentRowIndex = 0;

            FetchTableInformation();
            Read();

            return true;
        }

        return false;
    }
}

