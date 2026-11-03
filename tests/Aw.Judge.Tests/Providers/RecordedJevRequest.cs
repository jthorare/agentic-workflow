using System.Text;
using System.Text.Json.Nodes;

namespace Aw.Judge.Tests.Providers;

/// <summary>One request Jev was sent, read while the content was still alive: the headers that matter, and the body or its multipart parts.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Uri">The URL.</param>
/// <param name="Authorization">The Authorization header, as sent.</param>
/// <param name="ImagesHeader">The TypeSafe-Danger-Zone-Images header's value, or null when it was not sent.</param>
/// <param name="ContentType">The media type of the content ('application/json' or 'multipart/form-data').</param>
/// <param name="Body">The content's text (the whole multipart body when there are parts).</param>
/// <param name="Parts">The multipart parts in order; empty for a JSON body.</param>
internal sealed record RecordedJevRequest(
    HttpMethod Method, string? Uri, string? Authorization, string? ImagesHeader, string? ContentType, string Body, IReadOnlyList<RecordedJevRequest.Part> Parts)
{
    /// <summary>The request's JSON: the body, or the 'request' part of a multipart one.</summary>
    public JsonNode Json => JsonNode.Parse(Parts.Count == 0 ? Body : Encoding.UTF8.GetString(Parts.Single(p => p.Name == "request").Bytes))!;   // why: the provider only ever sends JSON

    public static async Task<RecordedJevRequest> Capture(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        return new RecordedJevRequest(
            request.Method,
            request.RequestUri?.ToString(),
            request.Headers.Authorization?.ToString(),
            Header(request, "TypeSafe-Danger-Zone-Images"),
            request.Content?.Headers.ContentType?.MediaType,
            body,
            await PartsOf(request.Content, cancellationToken));
    }

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? string.Join(',', values) : null;

    private static async Task<IReadOnlyList<Part>> PartsOf(HttpContent? content, CancellationToken cancellationToken)
    {
        var parts = new List<Part>();
        if (content is MultipartContent multipart)
        {
            foreach (var part in multipart)
            {
                parts.Add(new Part(
                    part.Headers.ContentDisposition?.Name?.Trim('"'),
                    part.Headers.ContentDisposition?.FileName?.Trim('"'),
                    part.Headers.ContentType?.MediaType,
                    await part.ReadAsByteArrayAsync(cancellationToken)));
            }
        }

        return parts;
    }

    /// <summary>One multipart part.</summary>
    /// <param name="Name">The form field name.</param>
    /// <param name="FileName">The file name, when the part is a file.</param>
    /// <param name="ContentType">The part's media type.</param>
    /// <param name="Bytes">The part's bytes.</param>
    internal sealed record Part(string? Name, string? FileName, string? ContentType, byte[] Bytes);
}
