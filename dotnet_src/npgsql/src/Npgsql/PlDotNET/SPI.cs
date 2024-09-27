using System;
using System.Runtime.InteropServices;
using PlDotNET.Common;

namespace Npgsql;

/// <summary>
/// Class used to import the functions defined in C
/// </summary>
public static class SPI
{
    /// <summary>
    /// C function declared in pldotnet_spi.h to call SPIPrepare
    /// See ::pldotnet_SPIPrepare().
    /// </summary>
    [DllImport("@PKG_LIBDIR/pldotnet.so")]
    public static extern void pldotnet_SPIPrepare(ref IntPtr cmdPointer, string command, int nargs, uint[] paramTypesOid, ref IntPtr errorData);

    /// <summary>
    /// C function declared in pldotnet_spi.h to call SPI_execute
    /// See ::pldotnet_SPIExecute().
    /// </summary>
    [DllImport("@PKG_LIBDIR/pldotnet.so")]
    public static extern IntPtr pldotnet_SPIExecute(string command, [MarshalAs(UnmanagedType.I1)] bool read_only, long count, ref IntPtr errorData);

    /// <summary>
    /// C function declared in pldotnet_spi.h to call SPI_execute_plan
    /// See ::pldotnet_SPIExecute().
    /// </summary>
    [DllImport("@PKG_LIBDIR/pldotnet.so")]
    public static extern IntPtr pldotnet_SPIExecutePlan(IntPtr plan, IntPtr[] paramValues, char[] nullmap, [MarshalAs(UnmanagedType.I1)] bool read_only, long count, ref IntPtr errorData);

    /// <summary>
    /// C function declared in pldotnet_spi.h to call SPI_commit
    /// See ::pldotnet_SPICommit().
    /// </summary>
    [DllImport("@PKG_LIBDIR/pldotnet.so")]
    public static extern void pldotnet_SPICommit(ref IntPtr errorData);

    /// <summary>
    /// C function declared in pldotnet_spi.h to call SPI_rollback
    /// See ::pldotnet_SPIRollback().
    /// </summary>
    [DllImport("@PKG_LIBDIR/pldotnet.so")]
    public static extern void pldotnet_SPIRollback(ref IntPtr errorData);

    /// <summary>
    /// C function declared in pldotnet_spi.h to retrieve the number of rows an columns
    /// See ::pldotnet_GetTableDimensions().
    /// </summary>
    [DllImport("@PKG_LIBDIR/pldotnet.so")]
    public static extern int pldotnet_GetTableColumnNumber(IntPtr tupleTable);

    /// <summary>
    /// C function declared in pldotnet_spi.h to retrieve table type OID
    /// See ::pldotnet_GetTableTypeID().
    /// </summary>
    [DllImport("@PKG_LIBDIR/pldotnet.so")]
    public static extern int pldotnet_GetTableTypeID(IntPtr tupleTable);

    /// <summary>
    /// C function declared in pldotnet_spi.h to populate the arrays with datums object
    /// and the nullmap of the current row
    /// See ::pldotnet_GetRow().
    /// </summary>
    [DllImport("@PKG_LIBDIR/pldotnet.so")]
    public static extern void pldotnet_GetRow(int row, IntPtr[] datums, byte[] isNull, IntPtr tupleTable);

    /// <summary>
    /// C function declared in pldotnet_spi.h to populate the arrays with the column types (OID values)
    /// and the column names
    /// See ::pldotnet_GetColProps().
    /// </summary>
    [DllImport("@PKG_LIBDIR/pldotnet.so")]
    public static extern void pldotnet_GetColProps(int[] columnTypes, IntPtr[] columnNames, int[] columnTypmods, int[] columnLens, IntPtr tupleTable);

    /// <summary>
    /// C function declared in pldotnet_main.h to return the PostgreSQL version.
    /// See ::pldotnet_GetPostgreSqlVersion().
    /// </summary>
    [DllImport("@PKG_LIBDIR/pldotnet.so")]
    public static extern IntPtr pldotnet_GetPostgreSqlVersion();

    /// <summary>
    /// C function declared in pldotnet_spi.h to transform a elevel to a string
    /// See ::pldotnet_ErrorSeverity().
    /// </summary>
    [DllImport("@PKG_LIBDIR/pldotnet.so")]
    public static extern IntPtr pldotnet_ErrorSeverity(int elevel);

    /// <summary>
    /// C function declared in pldotnet_spi.h to free the ErrorData pinter
    /// See ::pldotnet_FreeErrorData().
    /// </summary>
    [DllImport("@PKG_LIBDIR/pldotnet.so")]
    public static extern void pldotnet_FreeErrorData(IntPtr errorData);

    /// <summary>
    /// C function declared in pldotnet_spi.h to return the number of processed rows.
    /// See ::pldotnet_GetProcessedRowsNumber().
    /// </summary>
    [DllImport("@PKG_LIBDIR/pldotnet.so")]
    public static extern ulong pldotnet_GetProcessedRowsNumber();

    /// <summary>
    /// Execute a query inside PostgreSQL through calling pldotnet_SPIExecute.
    /// See ::pldotnet_SPIExecute().
    /// </summary>
    public static void Execute(string cmd)
    {
        IntPtr errorDataPtr = IntPtr.Zero;
        pldotnet_SPIExecute(cmd, false, 0, ref errorDataPtr);
        if (errorDataPtr != IntPtr.Zero)
        {
            Elog.Warning("The code fails in C!");
            SPIHelper.HandlePostgresqlError(errorDataPtr);
        }
    }
}
