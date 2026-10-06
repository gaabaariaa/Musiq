using Musiq.Domain.Entities;

namespace Musiq.Application.Abstractions;

public interface ILibraryQuery
{
    Task<IReadOnlyList<Song>> GetRecentlyAddedAsync(int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Song>> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Song>> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
