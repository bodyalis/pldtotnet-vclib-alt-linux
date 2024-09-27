namespace Npgsql;

/// <summary>
/// Represents a modified version of the NpgsqlMultiHostDataSource class that inherits from the NpgsqlDataSource class,
/// which has been modified by pldotnet and inherited from the original Npgsql class (<see cref="NpgsqlDataSourceOrig"/>).
/// </summary>
public class NpgsqlMultiHostDataSource : NpgsqlDataSource
{
    /// <summary>
    /// Constructor
    /// </summary>
    internal NpgsqlMultiHostDataSource() : base() {}

    internal NpgsqlMultiHostDataSource(NpgsqlConnectionStringBuilder settings, NpgsqlDataSourceConfiguration dataSourceConfig)
        : this() {}

    /// <summary>
    /// Returns an <see cref="NpgsqlDataSource" />
    /// </summary>
    public new NpgsqlMultiHostDataSource WithTargetSession(TargetSessionAttributes targetSessionAttributes)
        => (NpgsqlMultiHostDataSource) Create();
}
