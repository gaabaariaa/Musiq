using Musiq.Application.Abstractions;
using Musiq.Domain.Entities;

namespace Musiq.Infrastructure.Library;

public sealed class InMemoryLibraryQuery : ILibraryQuery
{
    public Task<IReadOnlyList<Song>> GetRecentlyAddedAsync(int limit, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Song>>(Array.Empty<Song>());
}
