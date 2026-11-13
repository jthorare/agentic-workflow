using System.Globalization;
using System.Net.Http.Headers;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.CodeIndex.Infrastructure.Embedding;

/// <summary>
/// Ollama on the loopback address as the embeddings layer's model (spec §5.6): no proxy, no redirect, an IP-literal URL, never a
/// cloud model, and the pinned model must be installed with the pinned digest. It never pulls or downloads. Errors carry codes
/// and counts only, never what the server said.
/// </summary>
internal sealed class OllamaEmbedder : IEmbedder, IDisposable
{
    private const string EmbedPath = "/api/embed";
    private const string TagsPath = "/api/tags";
    private const string Sha256Prefix = "sha256:";

    /// <summary>The model and digest in context/code-index.lock.</summary>
    internal static readonly ModelPin DefaultPin = new("nomic-embed-text:latest", "sha256:0a109f422b47e3a30ba2b10eca18548e944e8a23073ee3f3e947efcf3c45e59f");

    /// <summary>One text is cut at this many characters before it is sent: dense code runs 2 to 3 characters per token, and the model's context is 2,048 tokens (#3128).</summary>
    internal const int InputMaxCharacters = 2_500;

    private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _embedTimeout = TimeSpan.FromMinutes(10);

    private readonly HttpClient _http;
    private readonly ModelPin _pin;

#pragma warning disable CA2000 // the HttpClient owns the handler and this class disposes the HttpClient
    public OllamaEmbedder()
        : this(DefaultPin, new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
    {
    }
#pragma warning restore CA2000

    internal OllamaEmbedder(ModelPin pin, HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(pin);
        ArgumentNullException.ThrowIfNull(handler);
        _pin = pin;
        _http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 64 * 1024 * 1024 };
    }

    public string ModelFor(EmbeddingsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Model ?? _pin.Name;
    }

    public async ValueTask<Result<Unit>> Probe(EmbeddingsSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (EmbeddingTarget.Check(settings) is { } refused)
        {
            return refused;
        }

        var tags = await Send<TagsResponse>(HttpMethod.Get, Endpoint(settings, TagsPath), body: null, _probeTimeout, cancellationToken).ConfigureAwait(false);
        return tags.IsError ? tags.Error : Check(tags.Value, ModelFor(settings));
    }

    public async ValueTask<Result<IReadOnlyList<ReadOnlyMemory<float>>>> Embed(EmbeddingsSettings settings, IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(texts);
        if (EmbeddingTarget.Check(settings) is { } refused)
        {
            return refused;
        }

        if (texts.Count == 0)
        {
            return Results.Success<IReadOnlyList<ReadOnlyMemory<float>>>([]);
        }

        var cut = texts.Select(static t => Cut(t)).ToList();
        var whole = await EmbedBatch(settings, cut, cancellationToken).ConfigureAwait(false);
        if (!whole.IsError || !IsRejection(whole.Error))
        {
            return whole;
        }

        // The server refused something in the batch: ask for each text alone; a text it still refuses gets an empty vector, which the embeddings layer stores as a refused marker.
        return await EmbedEach(settings, cut, cancellationToken).ConfigureAwait(false);
    }

    // Cuts at InputMaxCharacters, one character earlier when that would leave half a surrogate pair.
    private static string Cut(string text)
        => text.Length <= InputMaxCharacters ? text : text[..(char.IsHighSurrogate(text[InputMaxCharacters - 1]) ? InputMaxCharacters - 1 : InputMaxCharacters)];

    private async ValueTask<Result<IReadOnlyList<ReadOnlyMemory<float>>>> EmbedEach(EmbeddingsSettings settings, List<string> texts, CancellationToken cancellationToken)
    {
        var vectors = new List<ReadOnlyMemory<float>>(texts.Count);
        foreach (var text in texts)
        {
            var one = await EmbedBatch(settings, [text], cancellationToken).ConfigureAwait(false);
            if (one.IsError && !IsRejection(one.Error))
            {
                return one.Error;
            }

            vectors.Add(one.IsError ? ReadOnlyMemory<float>.Empty : one.Value[0]);
        }

        return Results.Success<IReadOnlyList<ReadOnlyMemory<float>>>(vectors);
    }

    private static bool IsRejection(Error error) => string.Equals(error.Code, CodeIndexErrors.EmbeddingInputRejected.Code, StringComparison.Ordinal);

    private async ValueTask<Result<IReadOnlyList<ReadOnlyMemory<float>>>> EmbedBatch(EmbeddingsSettings settings, List<string> texts, CancellationToken cancellationToken)
    {
        var request = new EmbedRequest(ModelFor(settings), texts, Truncate: true);
        var answer = await Send<EmbedResponse>(HttpMethod.Post, Endpoint(settings, EmbedPath), request, _embedTimeout, cancellationToken).ConfigureAwait(false);
        return answer.IsError ? answer.Error : Vectors(answer.Value, texts.Count);
    }

    public void Dispose() => _http.Dispose();

    private static Uri Endpoint(EmbeddingsSettings settings, string path) => new(new Uri(settings.Url), path);

    private Result<Unit> Check(TagsResponse tags, string model)
    {
        var installed = (tags.Models ?? []).FirstOrDefault(m => string.Equals(m.Name, model, StringComparison.Ordinal));
        if (installed is null)
        {
            return CodeIndexErrors.PinMismatch.With("the embedding model is not installed; run scripts/install-deps.sh");
        }

        // Only the pinned model has a digest to hold it to; any other model the profile names just has to be installed.
        return string.Equals(model, _pin.Name, StringComparison.Ordinal) && !SameDigest(installed.Digest, _pin.Digest)
            ? CodeIndexErrors.PinMismatch.With("the embedding model's digest is not the pinned one; run scripts/install-deps.sh")
            : Unit.Value;
    }

    private static bool SameDigest(string? installed, string pinned)
        => installed is not null && string.Equals(WithoutPrefix(installed), WithoutPrefix(pinned), StringComparison.OrdinalIgnoreCase);

    private static string WithoutPrefix(string digest) => digest.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase) ? digest[Sha256Prefix.Length..] : digest;

    private static Result<IReadOnlyList<ReadOnlyMemory<float>>> Vectors(EmbedResponse answer, int expected)
    {
        var vectors = answer.Embeddings;
        if (vectors is null || vectors.Length != expected || vectors[0].Length == 0 || vectors.Any(v => v.Length != vectors[0].Length))
        {
            return CodeIndexErrors.EmbeddingServerFailed.With("the embedding server answered an unexpected shape");
        }

        return Results.Success<IReadOnlyList<ReadOnlyMemory<float>>>([.. vectors.Select(v => (ReadOnlyMemory<float>)v)]);
    }

    private static Error Failed(string what) => CodeIndexErrors.EmbeddingServerFailed.With(what);

    private async ValueTask<Result<T>> Send<T>(HttpMethod method, Uri uri, EmbedRequest? body, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            if (body is not null)
            {
                request.Content = new ByteArrayContent(AwJson.Serialize(body, OllamaWire.Options));
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            using var response = await _http.SendAsync(request, limit.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                return body is not null && status is 400 or 413 or 422
                    ? CodeIndexErrors.EmbeddingInputRejected.With("the embedding server refused the input (" + status.ToString(CultureInfo.InvariantCulture) + ")")
                    : Failed("the embedding server answered " + status.ToString(CultureInfo.InvariantCulture));
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(limit.Token).ConfigureAwait(false);
            var parsed = AwJson.Deserialize<T>(bytes, OllamaWire.Options);
            if (parsed.IsError)
            {
                return Failed("the embedding server answered an unexpected shape");
            }

            return parsed.Value;
        }
        catch (HttpRequestException)
        {
            return Failed("the embedding server did not answer");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed("the embedding server timed out");
        }
    }
}
