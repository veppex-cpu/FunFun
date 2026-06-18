namespace GameOfLife.Api.Models;

public sealed record UploadBoardRequest(IReadOnlyList<string>? Rows);
