using System.Text.Json;
using Aw.Sindri.Host;

namespace Aw.Sindri.Tests.Callers;

/// <summary>Raw requests with chosen caller headers, and the parts of the answer the caller tests check.</summary>
internal static class CallerRequests
{
    public static HttpRequestMessage Get(string path, params (string Name, string Value)[] headers) => With(new HttpRequestMessage(HttpMethod.Get, path), headers);

    public static HttpRequestMessage Post(string path, string? json, params (string Name, string Value)[] headers) =>
        With(new HttpRequestMessage(HttpMethod.Post, path) { Content = json is null ? null : new StringContent(json, System.Text.Encoding.UTF8, "application/json") }, headers);

    public static HttpRequestMessage Put(string path, string? json, params (string Name, string Value)[] headers) =>
        With(new HttpRequestMessage(HttpMethod.Put, path) { Content = json is null ? null : new StringContent(json, System.Text.Encoding.UTF8, "application/json") }, headers);

    /// <summary>An HTTP QUERY (the IETF safe method with a body, draft): what the subgraph route takes when its keys don't fit a URL.</summary>
    public static HttpRequestMessage Query(string path, string? json, params (string Name, string Value)[] headers) =>
        With(new HttpRequestMessage(new HttpMethod("QUERY"), path) { Content = json is null ? null : new StringContent(json, System.Text.Encoding.UTF8, "application/json") }, headers);

    public static (string Name, string Value)[] Human(string? token, string client = "cli") =>
        token is null
            ? [(SindriHeaders.Caller, "human"), (SindriHeaders.Terminal, "1"), (SindriHeaders.Client, client)]
            : [(SindriHeaders.Caller, "human"), (SindriHeaders.Terminal, "1"), (SindriHeaders.Client, client), (SindriHeaders.Token, token)];

    /// <summary>Sends <paramref name="request"/>: the status, the ProblemDetails title (null for another body) and the Sindri-Api-Version header.</summary>
    public static async Task<Answer> Ask(this HttpClient http, HttpRequestMessage request)
    {
        var ct = TestContext.Current.CancellationToken;
        using (request)
        {
            using var response = await http.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            var version = response.Headers.TryGetValues(SindriHeaders.ApiVersion, out var values) ? string.Join(',', values) : null;
            return new Answer(response.StatusCode, Title(text), version, text);
        }
    }

    private static string? Title(string text)
    {
        try
        {
            return JsonDocument.Parse(text).RootElement.TryGetProperty("title", out var title) ? title.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static HttpRequestMessage With(HttpRequestMessage request, (string Name, string Value)[] headers)
    {
        foreach (var (name, value) in headers)
        {
            _ = request.Headers.TryAddWithoutValidation(name, value);
        }

        return request;
    }
}
