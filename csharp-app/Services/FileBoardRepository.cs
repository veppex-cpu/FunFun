using System.Text.Json;
using GameOfLife.Api.Models;

namespace GameOfLife.Api.Services;

public sealed class FileBoardRepository : IBoardRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _storagePath;

    public FileBoardRepository(string storagePath)
    {
        _storagePath = storagePath;
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<StoredBoard?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = GetPath(id);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<StoredBoard>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(StoredBoard board, CancellationToken cancellationToken = default)
    {
        var finalPath = GetPath(board.Id);
        var tempPath = Path.Combine(_storagePath, $"{board.Id:N}.tmp");

        // Write to a temp file first so partial writes are not exposed as board files.
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, board, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        // Move into place only after serialization succeeds.
        File.Move(tempPath, finalPath, overwrite: true);
    }

    private string GetPath(Guid id) => Path.Combine(_storagePath, $"{id:N}.json");
}
