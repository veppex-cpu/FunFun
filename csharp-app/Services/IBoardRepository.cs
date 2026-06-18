using GameOfLife.Api.Models;

namespace GameOfLife.Api.Services;

public interface IBoardRepository
{
    Task<StoredBoard?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(StoredBoard board, CancellationToken cancellationToken = default);
}
