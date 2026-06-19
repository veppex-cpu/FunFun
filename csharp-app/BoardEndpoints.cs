using GameOfLife.Api.Models;
using GameOfLife.Api.Services;

namespace GameOfLife.Api;

public static class BoardEndpoints
{
    /// <summary>Uploads and persists a new board, returning the created board id and normalized rows.</summary>
    public static async Task<IResult> Upload(UploadBoardRequest request, BoardService service, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.UploadAsync(request, cancellationToken);
            return Results.Created($"/boards/{result.Id}", result);
        }
        catch (BadHttpRequestException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }
    }

    /// <summary>Returns the originally uploaded board for the supplied id.</summary>
    public static async Task<IResult> Get(Guid id, BoardService service, CancellationToken cancellationToken)
    {
        var board = await service.GetAsync(id, cancellationToken);
        return board is null ? Results.NotFound(new ErrorResponse($"Board '{id}' was not found.")) : Results.Ok(board);
    }

    /// <summary>Computes one generation from the originally uploaded board.</summary>
    public static async Task<IResult> GetNext(Guid id, BoardService service, CancellationToken cancellationToken)
    {
        var board = await service.GetNextAsync(id, cancellationToken);
        return board is null ? Results.NotFound(new ErrorResponse($"Board '{id}' was not found.")) : Results.Ok(board);
    }

    /// <summary>Computes a board state a requested number of generations from the original board.</summary>
    public static async Task<IResult> GetStateAfter(Guid id, int steps, BoardService service, CancellationToken cancellationToken)
    {
        if (steps < 0)
        {
            return Results.BadRequest(new ErrorResponse("Steps must be greater than or equal to 0."));
        }

        if (steps > BoardService.MaxSteps)
        {
            return Results.BadRequest(new ErrorResponse($"Steps cannot exceed {BoardService.MaxSteps}."));
        }

        var board = await service.GetStateAfterAsync(id, steps, cancellationToken);
        return board is null ? Results.NotFound(new ErrorResponse($"Board '{id}' was not found.")) : Results.Ok(board);
    }

    /// <summary>Searches for a stable still-life state within the requested attempt limit.</summary>
    public static async Task<IResult> GetFinal(Guid id, int? maxAttempts, BoardService service, CancellationToken cancellationToken)
    {
        if (maxAttempts is < 0)
        {
            return Results.BadRequest(new ErrorResponse("maxAttempts must be greater than or equal to 0."));
        }

        if (maxAttempts > BoardService.MaxFinalStateAttempts)
        {
            return Results.BadRequest(new ErrorResponse($"maxAttempts cannot exceed {BoardService.MaxFinalStateAttempts}."));
        }

        var result = await service.GetFinalStateAsync(id, maxAttempts ?? 100, cancellationToken);
        return result.Status switch
        {
            FinalStateStatus.Found => Results.Ok(result.Board),
            FinalStateStatus.BoardNotFound => Results.NotFound(new ErrorResponse($"Board '{id}' was not found.")),
            FinalStateStatus.CycleDetected => Results.UnprocessableEntity(new ErrorResponse(result.Error ?? "Board entered a cycle before stabilizing.")),
            _ => Results.UnprocessableEntity(new ErrorResponse(result.Error ?? "No final state was found within maxAttempts."))
        };
    }
}
