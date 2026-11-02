using System.Net;
using System.Text;
using System.Text.Json;

namespace Aw.Embeddings.Testing;

/// <summary>
/// A scripted Ollama: GET /api/tags lists <see cref="Models"/>, POST /api/embed answers a small deterministic vector per input, or
/// HTTP 400 "the input length exceeds the context length" when any input is longer than <see cref="MaxInputChars"/>. It records every call.
/// </summary>
public sealed class FakeOllamaHandler : HttpMessageHandler
{
    private readonly List<RecordedCall> _calls = [];

    /// <summary>The vector dimension the fake answers.</summary>
    public const int Dimension = 4;

    /// <summary>Gets the installed models, name to digest.</summary>
    public IDictionary<string, string> Models { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets the longest input, in characters, the fake accepts.</summary>
    public int MaxInputChars { get; set; } = int.MaxValue;

    /// <summary>Gets or sets a status that every /api/embed call answers instead, or null.</summary>
    public int? EmbedStatus { get; set; }

    /// <summary>Gets the calls so far, in order.</summary>
    public IReadOnlyList<RecordedCall> Calls => _calls;

    /// <summary>The vector the fake makes for a text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>Length, a checksum, the first character and one.</returns>
    public static float[] VectorOf(string text)
        => [text.Length, text.Sum(c => c) % 1000, text.Length == 0 ? 0 : text[0], 1];

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Get && path == "/api/tags")
        {
            _calls.Add(new RecordedCall(request.Method.Method, path, request.RequestUri.Host, Model: null, Truncate: null, Inputs: []));
            return Json(HttpStatusCode.OK, new { models = Models.Select(m => new { name = m.Key, digest = m.Value }) });
        }

        if (request.Method == HttpMethod.Post && path == "/api/embed")
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            var inputs = doc.RootElement.GetProperty("input").EnumerateArray().Select(e => e.GetString()!).ToList();
            var truncate = doc.RootElement.TryGetProperty("truncate", out var t) ? t.GetBoolean() : (bool?)null;
            _calls.Add(new RecordedCall(request.Method.Method, path, request.RequestUri.Host, doc.RootElement.GetProperty("model").GetString(), truncate, inputs));
            if (EmbedStatus is { } status)
            {
                return Json((HttpStatusCode)status, new { error = "scripted failure" });
            }

            return inputs.Exists(i => i.Length > MaxInputChars)
                ? Json(HttpStatusCode.BadRequest, new { error = "the input length exceeds the context length" })
                : Json(HttpStatusCode.OK, new { embeddings = inputs.Select(VectorOf) });
        }

        return Json(HttpStatusCode.NotFound, new { error = "not found" });
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body)
        => new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
}
