namespace GameOfLife.Api.Services;

public sealed class GameOfLifeEngine
{
    private const char Alive = 'O';
    private const char Dead = '.';

    public IReadOnlyList<string> Next(IReadOnlyList<string> rows)
    {
        var height = rows.Count;
        var width = rows[0].Length;
        var next = new char[height][];

        for (var y = 0; y < height; y++)
        {
            next[y] = new char[width];

            for (var x = 0; x < width; x++)
            {
                var liveNeighbors = CountLiveNeighbors(rows, x, y, width, height);
                var isAlive = rows[y][x] == Alive;

                next[y][x] = (isAlive, liveNeighbors) switch
                {
                    (true, 2 or 3) => Alive,
                    (false, 3) => Alive,
                    _ => Dead
                };
            }
        }

        return next.Select(row => new string(row)).ToArray();
    }

    public static bool AreEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        return left.Count == right.Count && left.SequenceEqual(right);
    }

    public static string Signature(IReadOnlyList<string> rows) => string.Join('\n', rows);

    private static int CountLiveNeighbors(IReadOnlyList<string> rows, int x, int y, int width, int height)
    {
        var count = 0;

        // Scan the 3x3 area around the cell, skipping the cell itself.
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var neighborX = x + dx;
                var neighborY = y + dy;
                // The board is finite; anything outside the uploaded grid is dead.
                if (neighborX < 0 || neighborX >= width || neighborY < 0 || neighborY >= height)
                {
                    continue;
                }

                if (rows[neighborY][neighborX] == Alive)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
