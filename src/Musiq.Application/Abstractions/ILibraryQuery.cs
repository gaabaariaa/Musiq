using Musiq.Domain.Entities;

namespace Musiq.Application.Abstractions;

public interface ILibraryQuery
{
    Task<IReadOnlyList<Song>> GetRecentlyAddedAsync(int limit, CancellationToken cancellationToken = default);
}
