using GameOfLife.Api.Models;

namespace GameOfLife.Api.Services;

public interface IBoardRepository
{
    /// <summary>Gets a stored board by id, or null when no readable board exists.</summary>
    Task<StoredBoard?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Saves a board so it can be retrieved after process restart.</summary>
    Task SaveAsync(StoredBoard board, CancellationToken cancellationToken = default);
}
