namespace GameOfLife.Api.Models;

public sealed record BoardDto(Guid Id, IReadOnlyList<string> Rows, int Generation);
