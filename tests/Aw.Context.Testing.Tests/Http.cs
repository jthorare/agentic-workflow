using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Tests;

/// <summary>A small HTTP client for the fake servers: sends a request, returns the status and the parsed body.</summary>
internal static class Http
{
    public static async Task<(HttpStatusCode Status, JsonNode? Body)> Post(Uri url, string? authorization, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        return await Send(request, authorization);
    }

    public static async Task<(HttpStatusCode Status, JsonNode? Body)> Get(Uri url, string? authorization)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await Send(request, authorization);
    }

    private static async Task<(HttpStatusCode Status, JsonNode? Body)> Send(HttpRequestMessage request, string? authorization)
    {
        if (authorization is not null)
        {
            _ = request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        using var client = new HttpClient();
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, text.Length == 0 ? null : JsonNode.Parse(text));
    }
}
