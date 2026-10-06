using Musiq.Domain.Entities;

namespace Musiq.Application.Abstractions;

public interface IScanLocationRepository
{
    Task<IReadOnlyList<ScanLocation>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ScanLocation> AddAsync(string path, CancellationToken cancellationToken = default);
    Task SetEnabledAsync(long id, bool isEnabled, CancellationToken cancellationToken = default);
    Task RemoveAsync(long id, CancellationToken cancellationToken = default);
    Task MarkScannedAsync(long id, DateTimeOffset scannedAtUtc, CancellationToken cancellationToken = default);
}
