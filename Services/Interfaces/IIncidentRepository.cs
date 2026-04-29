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
}
