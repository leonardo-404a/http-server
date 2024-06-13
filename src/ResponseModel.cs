using System.Net;

namespace http_server;

public class Response
{
    public HttpStatusCode StatusCode { get; init; }
    public string Content { get; init; }
    public string ContentType { get; set; } = "text/plain";
    public string? ContentEncoding { get; set; }
    public string ContentLength { get; set; }

    public string StatusLine =>
        $"HTTP/1.1 {(int)StatusCode} {(StatusCode.Equals(HttpStatusCode.NotFound) ? "Not Found" : StatusCode)}\r\n";

    public string Headers
    {
        get
        {
            var headers = new List<string> { $"Content-Type: {ContentType}" };

            var hasContentEncoding = ContentEncoding is not null;
            if (hasContentEncoding)
                headers.Add($"Content-Encoding: {ContentEncoding}");

            headers.Add($"Content-Length: {ContentLength}");

            if (!hasContentEncoding)
            {
                headers.Add(string.Empty);
                headers.Add(Content);
                headers.Add(string.Empty);
            }

            headers.Add(string.Empty);

            return string.Join("\r\n", headers);
        }
    }

    public static implicit operator string(Response response) => response.StatusLine + response.Headers;
}
