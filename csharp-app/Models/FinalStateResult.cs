namespace GameOfLife.Api.Models;

public enum FinalStateStatus
{
    Found,
    BoardNotFound,
    CycleDetected,
    MaxAttemptsExceeded
}

public sealed record FinalStateResult(FinalStateStatus Status, BoardDto? Board, string? Error);
