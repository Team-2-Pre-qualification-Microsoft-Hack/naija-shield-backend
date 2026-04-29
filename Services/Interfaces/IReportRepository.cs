using naija_shield_backend.Models;

namespace naija_shield_backend.Services.Interfaces;

public interface IReportRepository
{
    Task<Report> SaveAsync(Report report, CancellationToken cancellationToken = default);
    Task<Report?> GetByIdAsync(string id, string agencyType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Report>> GetRecentAsync(int limit = 20, CancellationToken cancellationToken = default);
}
