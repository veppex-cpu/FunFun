using GameOfLife.Api.Models;

namespace GameOfLife.Api.Services;

public sealed class BoardService(IBoardRepository repository, GameOfLifeEngine engine)
{
    public const int MaxSteps = 10_000;
    public const int MaxFinalStateAttempts = 10_000;

    /// <summary>Validates, normalizes, assigns an id, and persists a new board.</summary>
    public async Task<BoardDto> UploadAsync(UploadBoardRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedRows = BoardValidator.ValidateAndNormalize(request.Rows);
        var storedBoard = new StoredBoard(Guid.NewGuid(), normalizedRows, DateTimeOffset.UtcNow);

        await repository.SaveAsync(storedBoard, cancellationToken);

        return new BoardDto(storedBoard.Id, storedBoard.Rows, Generation: 0);
    }

    /// <summary>Returns the originally uploaded board without computing any generations.</summary>
    public async Task<BoardDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var board = await repository.GetAsync(id, cancellationToken);
        return board is null ? null : new BoardDto(board.Id, board.Rows, Generation: 0);
    }

    /// <summary>Returns the next generation for a stored board without mutating persisted state.</summary>
    public async Task<BoardDto?> GetNextAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await GetStateAfterAsync(id, steps: 1, cancellationToken);
    }

    /// <summary>Returns the board state after the requested number of generations.</summary>
    public async Task<BoardDto?> GetStateAfterAsync(Guid id, int steps, CancellationToken cancellationToken = default)
    {
        if (steps < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(steps), "Steps must be greater than or equal to 0.");
        }

        if (steps > MaxSteps)
        {
            throw new ArgumentOutOfRangeException(nameof(steps), $"Steps cannot exceed {MaxSteps}.");
        }

        var board = await repository.GetAsync(id, cancellationToken);
        if (board is null)
        {
            return null;
        }

        var rows = board.Rows;
        for (var generation = 0; generation < steps; generation++)
        {
            rows = engine.Next(rows);
        }

        return new BoardDto(board.Id, rows, steps);
    }

    /// <summary>Finds a stable still-life state or reports a cycle/max-attempt failure.</summary>
    public async Task<FinalStateResult> GetFinalStateAsync(Guid id, int maxAttempts, CancellationToken cancellationToken = default)
    {
        if (maxAttempts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "maxAttempts must be greater than or equal to 0.");
        }

        if (maxAttempts > MaxFinalStateAttempts)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), $"maxAttempts cannot exceed {MaxFinalStateAttempts}.");
        }

        var board = await repository.GetAsync(id, cancellationToken);
        if (board is null)
        {
            return new FinalStateResult(FinalStateStatus.BoardNotFound, null, null);
        }

        var seenStates = new HashSet<string> { GameOfLifeEngine.Signature(board.Rows) };
        var current = board.Rows;

        for (var generation = 1; generation <= maxAttempts; generation++)
        {
            var next = engine.Next(current);

            // A still-life is final when another generation would not change it.
            if (GameOfLifeEngine.AreEqual(current, next))
            {
                return new FinalStateResult(FinalStateStatus.Found, new BoardDto(board.Id, next, generation), null);
            }

            var signature = GameOfLifeEngine.Signature(next);
            // Repeated signatures identify oscillators/cycles before maxAttempts is exhausted.
            if (!seenStates.Add(signature))
            {
                return new FinalStateResult(
                    FinalStateStatus.CycleDetected,
                    null,
                    $"No final state was found because the board entered a repeated cycle at generation {generation}.");
            }

            current = next;
        }

        return new FinalStateResult(
            FinalStateStatus.MaxAttemptsExceeded,
            null,
            $"No final state was found within maxAttempts ({maxAttempts}).");
    }
}
