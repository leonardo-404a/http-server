using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace http_server;

public static class ResponseHandler
{
    private const int BufferSize = 1024;
    private static readonly Response NotFoundResponse = ResponseBuilder(HttpStatusCode.NotFound);
    private static readonly IReadOnlyCollection<string> ValidEncodings = new[] { "gzip" };
    private static readonly string? FileStoragePath = null;

    internal static async Task ServeAsync(TcpClient client)
    {
        var response = NotFoundResponse;
        var stream = client.GetStream();

        try
        {
            var requestBuffer = new byte[BufferSize];
            _ = await stream.ReadAsync(requestBuffer);

            var request = Encoding.UTF8.GetString(requestBuffer).TrimEnd('\0');
            var requestParams = request.Split("\r\n");
            var requestLine = requestParams[0].Split(' ');
            var method = requestLine[0];
            var address = requestLine[1];

            var headers = ParseHeaders(requestParams);

            response = address switch
            {
                "/" => ResponseBuilder(HttpStatusCode.OK, string.Empty),
                _ when address.StartsWith("/echo") => CreateEchoResponse(address, headers),
                _ when address.StartsWith("/user-agent") => CreateUserAgentResponse(headers),
                _ when address.StartsWith("/files") => await CreateFilesResponse(method, address, requestParams),
                _ => NotFoundResponse
            };

        }
        catch (Exception e)
        {
            Console.WriteLine($"Error {e} occurred");
        }
        finally
        {
            var responseData = CreateResponseData(response);
            await stream.WriteAsync(responseData);
        }
    }

    private static byte[] CreateResponseData(Response response)
    {
        var responseData = Encoding.UTF8.GetBytes(response);

        if (string.IsNullOrEmpty(response.ContentEncoding))
            return responseData;

        var compressedData = CompressContent(response.Content, response.ContentEncoding);

        response.ContentLength = compressedData.Length.ToString();
        responseData = Encoding.UTF8.GetBytes(response.StatusLine + response.Headers + "\r\n");
        return [.. responseData, .. compressedData];
    }

    private static Dictionary<string, string> ParseHeaders(IReadOnlyList<string> requestParams)
    {
        Dictionary<string, string> headers = [];
        for (var index = 2; index < requestParams.Count; index++)
        {
            if (!requestParams[index].Contains(':') || string.IsNullOrEmpty(requestParams[index])) continue;

            var header = requestParams[index].Split(':');
            headers.Add(header[0].ToLower(), header[1].Trim());
        }

        return headers;
    }

    private static async Task<Response> CreateFilesResponse(string method, string address, IEnumerable<string> requestParams)
    {
        var argv = Environment.GetCommandLineArgs();
        var currentDirectory = FileStoragePath;
        currentDirectory ??= argv.Length > 2 ? argv[2] : Directory.GetCurrentDirectory();

        var filePath = Path.Combine(currentDirectory, address.Split("/")[2]);

        if (method.Equals("POST", StringComparison.InvariantCultureIgnoreCase))
        {
            var fileContent = requestParams.Last();
            await File.WriteAllTextAsync(filePath, fileContent);
            return ResponseBuilder(HttpStatusCode.Created, string.Empty);
        }

        if (!File.Exists(filePath)) return NotFoundResponse;
        {
            var fileContent = await File.ReadAllTextAsync(filePath);
            return ResponseBuilder(HttpStatusCode.OK, fileContent, "application/octet-stream");
        }
    }

    private static Response CreateUserAgentResponse(IReadOnlyDictionary<string, string> headers)
    {
        var userAgent = headers.GetValueOrDefault("user-agent", "Unknown");
        return ResponseBuilder(HttpStatusCode.OK, userAgent);
    }

    private static Response CreateEchoResponse(string address, IReadOnlyDictionary<string, string> headers)
    {
        var parts = address.Split('/');
        var echoMessage = parts.Length > 2 ? parts[2] : string.Empty;

        var acceptedEncodings = headers.GetValueOrDefault("accept-encoding", string.Empty).Split(", ");
        var acceptedEncoding = acceptedEncodings.FirstOrDefault(encoding => ValidEncodings.Contains(encoding));
        string? contentEncoding = null;

        if (string.IsNullOrEmpty(acceptedEncoding) is false)
            contentEncoding = acceptedEncoding;

        return ResponseBuilder(HttpStatusCode.OK, echoMessage, contentEncoding: contentEncoding);
    }

    private static Response ResponseBuilder(HttpStatusCode statusCode, string content = "", string contentType = "text/plain", string? contentEncoding = null, string? contentLength = null) =>
        new()
        {
            StatusCode = statusCode,
            Content = content,
            ContentType = contentType,
            ContentEncoding = contentEncoding,
            ContentLength = contentLength ?? content.Length.ToString()
        };

    private static byte[] CompressContent(string content, string encoding)
    {
        var contentBytes = Encoding.UTF8.GetBytes(content);
        using var outputStream = new MemoryStream();
        using var compressionStream = encoding switch
        {
            "gzip" => new GZipStream(outputStream, CompressionMode.Compress, true),
            _ => throw new NotSupportedException("Unsupported encoding")
        };

        compressionStream.Write(contentBytes, 0, contentBytes.Length);
        compressionStream.Flush();
        compressionStream.Close();
        return outputStream.ToArray();
    }
}