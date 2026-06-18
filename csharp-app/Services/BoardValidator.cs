namespace GameOfLife.Api.Services;

public static class BoardValidator
{
    public const int MaxRows = 2_000;
    public const int MaxColumns = 2_000;
    public const int MaxCells = 1_000_000;

    private const char Alive = 'O';
    private const char Dead = '.';

    /// <summary>Validates board shape, limits, and cell characters, then normalizes accepted aliases.</summary>
    public static IReadOnlyList<string> ValidateAndNormalize(IReadOnlyList<string>? rows)
    {
        if (rows is null || rows.Count == 0)
        {
            throw new BadHttpRequestException("Board must contain at least one row.");
        }

        if (rows.Any(row => row is null))
        {
            throw new BadHttpRequestException("Board rows cannot be null.");
        }

        if (rows.Count > MaxRows)
        {
            throw new BadHttpRequestException($"Board cannot contain more than {MaxRows} rows.");
        }

        var width = rows[0].Length;
        if (width == 0)
        {
            throw new BadHttpRequestException("Board rows cannot be empty.");
        }

        if (width > MaxColumns)
        {
            throw new BadHttpRequestException($"Board cannot contain more than {MaxColumns} columns.");
        }

        if ((long)rows.Count * width > MaxCells)
        {
            throw new BadHttpRequestException($"Board cannot contain more than {MaxCells} cells.");
        }

        var normalizedRows = new List<string>(rows.Count);
        foreach (var row in rows)
        {
            if (row.Length != width)
            {
                throw new BadHttpRequestException("Board must be rectangular. Every row must have the same length.");
            }

            var normalized = row
                .Replace('1', Alive)
                .Replace('0', Dead)
                .Replace('X', Alive)
                .Replace('x', Alive)
                .Replace('*', Alive);

            if (normalized.Any(cell => cell is not Alive and not Dead))
            {
                throw new BadHttpRequestException("Board cells must use '.' for dead cells and 'O' for live cells.");
            }

            normalizedRows.Add(normalized);
        }

        return normalizedRows;
    }
}
