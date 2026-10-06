using Musiq.Application.Abstractions;
using Musiq.Domain.Entities;

namespace Musiq.Infrastructure.Library;

public sealed class InMemoryLibraryQuery : ILibraryQuery
{
    public Task<IReadOnlyList<Song>> GetRecentlyAddedAsync(
        int limit,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Song>>(Array.Empty<Song>());

    public Task<IReadOnlyList<Song>> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Song>>(Array.Empty<Song>());

    public Task<IReadOnlyList<Song>> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Song>>(Array.Empty<Song>());
}
