using naija_shield_backend.Models;

namespace naija_shield_backend.Services.Interfaces;

/// <summary>
/// Persists and retrieves <see cref="ThreatIncident"/> documents
/// in the backing store (Azure Cosmos DB).
/// </summary>
public interface IIncidentRepository
{
    /// <summary>
    /// Writes <paramref name="incident"/> to the Cosmos DB container and
    /// returns the saved document (including any server-side modifications).
    /// The container's partition key path is <c>/channel</c>.
    /// </summary>
    /// <param name="incident">
    /// Fully populated incident. The <c>Id</c> and <c>Channel</c> fields
    /// must be set by the caller before passing to this method.
    /// </param>
    /// <param name="cancellationToken">Propagates notification that operation should be cancelled.</param>
    Task<ThreatIncident> SaveAsync(
        ThreatIncident incident,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent incidents across all channels, ordered newest first.
    /// Used by the dashboard threat feed table and geospatial heatmap.
    /// </summary>
    Task<IReadOnlyList<ThreatIncident>> GetRecentAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all incidents whose Cosmos <c>_ts</c> falls within
    /// [<paramref name="from"/>, <paramref name="to"/>], ordered newest first.
    /// Used by the report generator to scope data to a reporting period.
    /// </summary>
    Task<IReadOnlyList<ThreatIncident>> GetByDateRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all incidents where the <c>from</c> field matches <paramref name="phone"/>,
    /// ordered newest first. Used by the Reputation API to build a number's history.
    /// </summary>
    Task<IReadOnlyList<ThreatIncident>> GetByPhoneAsync(
        string phone,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
