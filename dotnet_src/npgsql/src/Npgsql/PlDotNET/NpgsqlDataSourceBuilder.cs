using System;
using System.Threading.Tasks;

namespace Npgsql;

/// <summary>
/// Represents a modified version of the NpgsqlDataSourceBuilder class that inherits from the original NpgsqlDataSourceBuilder class
/// (<see cref="NpgsqlDataSourceBuilderOrig"/>) provided by Npgsql to work with pldotnet procedural language.
/// </summary>
public class NpgsqlDataSourceBuilder : NpgsqlDataSourceBuilderOrig
{
    /// <summary>
    /// Constructor
    /// </summary>
    public NpgsqlDataSourceBuilder(string? connectionString = null) : base()
    {
    }

    /// <summary>
    /// Builds and returns an <see cref="NpgsqlDataSource" />
    /// </summary>
    public new NpgsqlDataSource Build()
        => NpgsqlDataSource.Create();

    /// <summary>
    /// Builds and returns a <see cref="NpgsqlMultiHostDataSource" />
    /// </summary>
    public new NpgsqlMultiHostDataSource BuildMultiHost()
        => (NpgsqlMultiHostDataSource) NpgsqlDataSource.Create();

    // TODO: check if we need this method
    // /// <summary>
    // /// Register a connection initializer, which allows executing arbitrary commands when a physical database connection is first opened.
    // /// </summary>
    // public NpgsqlDataSourceBuilder UsePhysicalConnectionInitializer(
    //     Action<NpgsqlConnection>? connectionInitializer,
    //     Func<NpgsqlConnection, Task>? connectionInitializerAsync)
    // {
    //     return this;
    // }
}
