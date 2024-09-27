using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NpgsqlTypes;
using PlDotNET.Common;
using PlDotNET.Handler;

namespace Npgsql;

#pragma warning disable CS8604

/// <summary>
/// Contains SPIHelper methods used in the modified Npgsql classes of pldotnet.
/// </summary>
public static class SPIHelper
{
    /// <summary>
    /// Struct to hold values about the error from C
    /// </summary>
    public struct ErrorData
    {
        /// <summary>
        /// error level
        /// </summary>
        public int elevel;

        /// <summary>
        /// will report to server log?
        /// </summary>
        [MarshalAs(UnmanagedType.I1)]
        public bool output_to_server;

        /// <summary>
        /// will report to client?
        /// </summary>
        [MarshalAs(UnmanagedType.I1)]
        public bool output_to_client;

        /// <summary>
        /// true to prevent STATEMENT: inclusion
        /// </summary>
        [MarshalAs(UnmanagedType.I1)]
        public bool hide_stmt;

        /// <summary>
        /// true to prevent CONTEXT: inclusion
        /// </summary>
        [MarshalAs(UnmanagedType.I1)]
        public bool hide_ctx;

        /// <summary>
        /// __FILE__ of ereport() call
        /// </summary>
        public IntPtr filename;

        /// <summary>
        /// __LINE__ of ereport() call
        /// </summary>
        public int lineno;

        /// <summary>
        /// __func__ of ereport() call
        /// </summary>
        public IntPtr funcname;

        /// <summary>
        /// message domain
        /// </summary>
        public IntPtr domain;

        /// <summary>
        /// message domain for context message
        /// </summary>
        public IntPtr context_domain;

        /// <summary>
        /// encoded ERRSTATE
        /// </summary>
        public int sqlerrcode;

        /// <summary>
        /// primary error message (translated)
        /// </summary>
        public IntPtr message;

        /// <summary>
        /// detail error message
        /// </summary>
        public IntPtr detail;

        /// <summary>
        /// detail error message for server log only
        /// </summary>
        public IntPtr detail_log;

        /// <summary>
        /// hint message
        /// </summary>
        public IntPtr hint;

        /// <summary>
        /// context message
        /// </summary>
        public IntPtr context;

        /// <summary>
        /// backtrace
        /// </summary>
        public IntPtr backtrace;

        /// <summary>
        /// primary message's id (original string)
        /// </summary>
        public IntPtr message_id;

        /// <summary>
        /// name of schema
        /// </summary>
        public IntPtr schema_name;

        /// <summary>
        /// name of table
        /// </summary>
        public IntPtr table_name;

        /// <summary>
        /// name of column
        /// </summary>
        public IntPtr column_name;

        /// <summary>
        /// name of datatype
        /// </summary>
        public IntPtr datatype_name;

        /// <summary>
        /// name of constraint
        /// </summary>
        public IntPtr constraint_name;

        /// <summary>
        /// cursor index into query string
        /// </summary>
        public int cursorpos;

        /// <summary>
        /// cursor index into internalquery
        /// </summary>
        public int internalpos;

        /// <summary>
        /// text of internally-generated query
        /// </summary>
        public IntPtr internalquery;

        /// <summary>
        /// errno at entry
        /// </summary>
        public int saved_errno;

        /// <summary>
        /// context containing associated non-constant strings
        /// </summary>
        public IntPtr assoc_context;
    }

    /// <summary>
    /// context containing associated non-constant strings
    /// </summary>
    public static unsafe void HandlePostgresqlError(IntPtr errorDataPtr)
    {
        ErrorData errorData = Marshal.PtrToStructure<ErrorData>(errorDataPtr);

        IntPtr severityPtr = SPI.pldotnet_ErrorSeverity(errorData.elevel);
        string severity = Marshal.PtrToStringAuto(severityPtr) ?? string.Empty;

        SPI.pldotnet_FreeErrorData(errorDataPtr);

        throw new PostgresException(
            messageText: Marshal.PtrToStringAuto(errorData.message),
            severity: severity,
            invariantSeverity: severity,
            sqlState: "12345",
            // This may be it, but it's still need to confirm the behaviour
            // sqlState: errorData.sqlerrcode.ToString(),
            detail: Marshal.PtrToStringAuto(errorData.detail),
            hint: Marshal.PtrToStringAuto(errorData.hint),
            position: errorData.cursorpos,
            internalPosition: errorData.internalpos,
            internalQuery: Marshal.PtrToStringAuto(errorData.internalquery),
            where: null,
            schemaName: Marshal.PtrToStringAuto(errorData.schema_name),
            tableName: Marshal.PtrToStringAuto(errorData.table_name),
            columnName: Marshal.PtrToStringAuto(errorData.column_name),
            dataTypeName: Marshal.PtrToStringAuto(errorData.datatype_name),
            constraintName: Marshal.PtrToStringAuto(errorData.constraint_name),
            file: Marshal.PtrToStringAuto(errorData.filename),
            line: errorData.lineno.ToString(),
            routine: null
        );
    }
}