using System.Collections.Concurrent;

namespace Npgsql;

/// <summary>
/// Represents a modified version of the (<see cref="PoolManagerOrig"/>) class
/// provided by Npgsql to work with pldotnet procedural language.
/// </summary>
public static class PoolManager
{
    /// <summary>
    /// Defines the pools
    /// </summary>
    public static ConcurrentDictionary<string, NpgsqlDataSource> Pools { get; } = new();

    /// <summary>
    /// Function to override the original behavior
    /// </summary>
    public static void Clear(string connString)
    {
    }

    /// <summary>
    /// Function to override the original behavior
    /// </summary>
    public static void ClearAll()
    {
    }

    /// <summary>
    /// Constructor
    /// </summary>
    static PoolManager()
    {
    }

    /// <summary>
    /// Function to override the original behavior
    /// </summary>
    public static void Reset()
    {
    }
}
