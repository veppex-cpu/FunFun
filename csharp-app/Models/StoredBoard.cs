namespace GameOfLife.Api.Models;

public sealed record StoredBoard(Guid Id, IReadOnlyList<string> Rows, DateTimeOffset CreatedAt);
