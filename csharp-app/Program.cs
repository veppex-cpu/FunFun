using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GameOfLife.Api.Models;
using GameOfLife.Api.Services;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true
};
var url = GetUrl(args);
var storagePath = Environment.GetEnvironmentVariable("GAME_OF_LIFE_STORAGE")
    ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "data", "boards");

var service = new BoardService(new FileBoardRepository(Path.GetFullPath(storagePath)), new GameOfLifeEngine());
var listener = new TcpListener(IPAddress.Parse(url.Host), url.Port);

listener.Start();
Console.WriteLine($"Game of Life API listening on {url}");

while (true)
{
    var client = await listener.AcceptTcpClientAsync();
    // Keep the listener responsive while each request is processed.
    _ = Task.Run(() => HandleClientAsync(client, service));
}

Uri GetUrl(string[] args)
{
    // Accept the same --urls shape developers expect from dotnet-hosted services.
    var url = GetArgValue(args, "--urls")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
        ?? "http://127.0.0.1:5010";

    var firstUrl = url.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
    return new Uri(firstUrl);
}

string? GetArgValue(string[] args, string name)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == name && i + 1 < args.Length)
        {
            return args[i + 1];
        }

        if (args[i].StartsWith($"{name}=", StringComparison.Ordinal))
        {
            return args[i][(name.Length + 1)..];
        }
    }

    return null;
}

async Task HandleClientAsync(TcpClient client, BoardService service)
{
    // One request per TCP connection keeps the custom transport simple.
    await using var stream = client.GetStream();

    try
    {
        var request = await ReadRequestAsync(stream);
        if (request is null)
        {
            return;
        }

        var response = await RouteAsync(request, service);
        await WriteJsonResponseAsync(stream, response.StatusCode, response.Body, response.Location);
    }
    catch (BadHttpRequestException ex)
    {
        await WriteJsonResponseAsync(stream, 400, new ErrorResponse(ex.Message));
    }
    catch (Exception ex)
    {
        await WriteJsonResponseAsync(stream, 500, new ErrorResponse($"Unexpected server error: {ex.Message}"));
    }
}

async Task<HttpRequest?> ReadRequestAsync(NetworkStream stream)
{
    // Parse only the small HTTP subset this API needs: request line, headers, and Content-Length body.
    var buffer = new byte[8192];
    var received = 0;
    var headerEnd = -1;

    // Read until the HTTP header terminator; the body may arrive in the same packet.
    while (received < buffer.Length)
    {
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(received, buffer.Length - received));
        if (bytesRead == 0)
        {
            return null;
        }

        received += bytesRead;
        headerEnd = IndexOfHeaderEnd(buffer, received);
        if (headerEnd >= 0)
        {
            break;
        }
    }

    if (headerEnd < 0)
    {
        throw new BadHttpRequestException("HTTP headers are too large or incomplete.");
    }

    var headerText = Encoding.ASCII.GetString(buffer, 0, headerEnd);
    var headerLines = headerText.Split("\r\n", StringSplitOptions.None);
    var requestLine = headerLines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (requestLine.Length < 2)
    {
        return null;
    }

    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var line in headerLines.Skip(1))
    {
        var separator = line.IndexOf(':', StringComparison.Ordinal);
        if (separator > 0)
        {
            headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }
    }

    var contentLength = 0;
    if (headers.TryGetValue("Content-Length", out var contentLengthValue)
        && (!int.TryParse(contentLengthValue, out contentLength) || contentLength < 0))
    {
        throw new BadHttpRequestException("Content-Length must be a non-negative integer.");
    }

    var bodyStart = headerEnd + 4;
    var bodyBytesReceived = received - bodyStart;
    var body = new byte[contentLength];

    if (bodyBytesReceived > 0)
    {
        Array.Copy(buffer, bodyStart, body, 0, Math.Min(bodyBytesReceived, contentLength));
    }

    while (bodyBytesReceived < contentLength)
    {
        var bytesRead = await stream.ReadAsync(body.AsMemory(bodyBytesReceived, contentLength - bodyBytesReceived));
        if (bytesRead == 0)
        {
            break;
        }

        bodyBytesReceived += bytesRead;
    }

    return new HttpRequest(requestLine[0], requestLine[1], body);
}

int IndexOfHeaderEnd(byte[] buffer, int length)
{
    for (var i = 3; i < length; i++)
    {
        if (buffer[i - 3] == '\r' && buffer[i - 2] == '\n' && buffer[i - 1] == '\r' && buffer[i] == '\n')
        {
            return i - 3;
        }
    }

    return -1;
}

async Task<HttpResponse> RouteAsync(HttpRequest request, BoardService service)
{
    // The transport is intentionally small, so routing is explicit and matches the documented API only.
    var pathAndQuery = request.Target.Split('?', 2);
    var path = pathAndQuery[0].TrimEnd('/');
    var query = pathAndQuery.Length == 2 ? ParseQuery(pathAndQuery[1]) : new Dictionary<string, string>();
    var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

    try
    {
        if (request.Method == "POST" && path == "/boards")
        {
            var uploadRequest = JsonSerializer.Deserialize<UploadBoardRequest>(request.Body, jsonOptions)
                ?? new UploadBoardRequest(null);
            var uploaded = await service.UploadAsync(uploadRequest);
            return new HttpResponse(201, uploaded, $"/boards/{uploaded.Id}");
        }

        if (request.Method == "GET" && segments.Length >= 2 && segments[0] == "boards" && Guid.TryParse(segments[1], out var id))
        {
            if (segments.Length == 2)
            {
                var board = await service.GetAsync(id);
                return board is null
                    ? new HttpResponse(404, new ErrorResponse($"Board '{id}' was not found."))
                    : new HttpResponse(200, board);
            }

            if (segments.Length == 3 && segments[2] == "next")
            {
                var board = await service.GetNextAsync(id);
                return board is null
                    ? new HttpResponse(404, new ErrorResponse($"Board '{id}' was not found."))
                    : new HttpResponse(200, board);
            }

            if (segments.Length == 4 && segments[2] == "states")
            {
                if (!int.TryParse(segments[3], out var steps))
                {
                    return new HttpResponse(400, new ErrorResponse("Steps must be an integer."));
                }

                if (steps < 0)
                {
                    return new HttpResponse(400, new ErrorResponse("Steps must be greater than or equal to 0."));
                }

                if (steps > BoardService.MaxSteps)
                {
                    return new HttpResponse(400, new ErrorResponse($"Steps cannot exceed {BoardService.MaxSteps}."));
                }

                var board = await service.GetStateAfterAsync(id, steps);
                return board is null
                    ? new HttpResponse(404, new ErrorResponse($"Board '{id}' was not found."))
                    : new HttpResponse(200, board);
            }

            if (segments.Length == 3 && segments[2] == "final")
            {
                var maxAttempts = 100;
                if (query.TryGetValue("maxAttempts", out var maxAttemptsValue)
                    && !int.TryParse(maxAttemptsValue, out maxAttempts))
                {
                    return new HttpResponse(400, new ErrorResponse("maxAttempts must be an integer."));
                }

                if (maxAttempts < 0)
                {
                    return new HttpResponse(400, new ErrorResponse("maxAttempts must be greater than or equal to 0."));
                }

                if (maxAttempts > BoardService.MaxFinalStateAttempts)
                {
                    return new HttpResponse(400, new ErrorResponse($"maxAttempts cannot exceed {BoardService.MaxFinalStateAttempts}."));
                }

                var result = await service.GetFinalStateAsync(id, maxAttempts);
                return result.Status switch
                {
                    FinalStateStatus.Found => new HttpResponse(200, result.Board),
                    FinalStateStatus.BoardNotFound => new HttpResponse(404, new ErrorResponse($"Board '{id}' was not found.")),
                    _ => new HttpResponse(422, new ErrorResponse(result.Error ?? "No final state was found."))
                };
            }
        }

        return new HttpResponse(404, new ErrorResponse("Endpoint was not found."));
    }
    catch (BadHttpRequestException ex)
    {
        return new HttpResponse(400, new ErrorResponse(ex.Message));
    }
    catch (JsonException ex)
    {
        return new HttpResponse(400, new ErrorResponse($"Invalid JSON: {ex.Message}"));
    }
}

Dictionary<string, string> ParseQuery(string query)
{
    // Query parameters are decoded into a case-insensitive lookup for endpoint options.
    return query
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .Where(parts => parts.Length == 2)
        .ToDictionary(
            parts => WebUtility.UrlDecode(parts[0]),
            parts => WebUtility.UrlDecode(parts[1]),
            StringComparer.OrdinalIgnoreCase);
}

async Task WriteJsonResponseAsync(NetworkStream stream, int statusCode, object? body, string? location = null)
{
    // Responses are always JSON, including errors, so clients get one predictable shape.
    var payload = JsonSerializer.SerializeToUtf8Bytes(body, jsonOptions);
    var reason = statusCode switch
    {
        200 => "OK",
        201 => "Created",
        400 => "Bad Request",
        404 => "Not Found",
        422 => "Unprocessable Entity",
        _ => "Internal Server Error"
    };

    var headers = new StringBuilder()
        .Append("HTTP/1.1 ").Append(statusCode).Append(' ').Append(reason).Append("\r\n")
        .Append("Content-Type: application/json; charset=utf-8\r\n")
        .Append("Content-Length: ").Append(payload.Length).Append("\r\n")
        .Append("Connection: close\r\n");

    if (location is not null)
    {
        headers.Append("Location: ").Append(location).Append("\r\n");
    }

    headers.Append("\r\n");

    await stream.WriteAsync(Encoding.ASCII.GetBytes(headers.ToString()));
    await stream.WriteAsync(payload);
}

public partial class Program;

internal sealed record HttpRequest(string Method, string Target, byte[] Body);

internal sealed record HttpResponse(int StatusCode, object? Body, string? Location = null);
