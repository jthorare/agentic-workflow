using System.Globalization;
using System.Net.Http.Headers;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Embeddings;

/// <summary>
/// Ollama on the loopback address (spec §5.6): an IP-literal URL, no proxy, no redirect, never a cloud model, and the pinned
/// model must be installed with the pinned digest; it never pulls. Texts go in batches of 32 with truncate off; an
/// input the model's context cannot hold is halved and retried once. Errors carry codes and counts only, never what the server said.
/// </summary>
public sealed class OllamaEmbedder : IEmbedder, IDisposable
{
    /// <summary>The batch size.</summary>
    public const int BatchSize = 32;

    private const string EmbedPath = "/api/embed";
    private const string TagsPath = "/api/tags";
    private const string Sha256Prefix = "sha256:";
    private const string ContextLengthMessage = "exceeds the context length";

    private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _embedTimeout = TimeSpan.FromMinutes(10);

    private readonly OllamaOptions _options;
    private readonly HttpClient _http;
    private bool _verified;

#pragma warning disable CA2000 // the HttpClient owns the handler and this class disposes the HttpClient
    /// <summary>Makes an embedder over a loopback-only client with no proxy and no redirects.</summary>
    /// <param name="options">The server, model and pin.</param>
    public OllamaEmbedder(OllamaOptions options)
        : this(options, new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
    {
    }
#pragma warning restore CA2000

    internal OllamaEmbedder(OllamaOptions options, HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);
        _options = options;
        _http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 64 * 1024 * 1024 };
    }

    /// <summary>Whether a URL is an http URL on 127.0.0.1 or [::1] with no credentials.</summary>
    /// <param name="url">The URL.</param>
    /// <returns>True when it is.</returns>
    public static bool IsLoopbackUrl(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            && uri.UserInfo.Length == 0
            && (string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal) || string.Equals(uri.Host, "[::1]", StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken)
    {
        if (CheckTarget() is { } refused)
        {
            return refused;
        }

        var response = await Send(HttpMethod.Get, TagsPath, body: null, _probeTimeout, cancellationToken).ConfigureAwait(false);
        if (response.IsError)
        {
            return response.Error;
        }

        var tags = Parse<TagsResponse>(response.Value.Body);
        if (tags.IsError || response.Value.Status != 200)
        {
            return Failed(response.Value.Status == 200 ? "the embedding server answered an unexpected shape" : Status(response.Value.Status));
        }

        if (CheckInstalled(tags.Value) is { } mismatch)
        {
            return mismatch;
        }

        _verified = true;
        return Unit.Value;
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<ReadOnlyMemory<float>>>> Embed(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (CheckTarget() is { } refused)
        {
            return refused;
        }

        if (texts.Count == 0)
        {
            return Results.Success<IReadOnlyList<ReadOnlyMemory<float>>>([]);
        }

        if (!_verified && await Probe(cancellationToken).ConfigureAwait(false) is { IsError: true } probe)
        {
            return probe.Error;
        }

        var vectors = new List<ReadOnlyMemory<float>>(texts.Count);
        for (var start = 0; start < texts.Count; start += BatchSize)
        {
            var batch = await EmbedBatch([.. texts.Skip(start).Take(BatchSize)], cancellationToken).ConfigureAwait(false);
            if (batch.IsError)
            {
                return batch.Error;
            }

            vectors.AddRange(batch.Value);
        }

        return vectors.Exists(v => v.Length != vectors[0].Length)
            ? Failed("the embedding server answered vectors of different sizes")
            : vectors;
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    private static bool SameDigest(string? installed, string pinned)
        => installed is not null && string.Equals(WithoutPrefix(installed), WithoutPrefix(pinned), StringComparison.OrdinalIgnoreCase);

    private static string WithoutPrefix(string digest) => digest.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase) ? digest[Sha256Prefix.Length..] : digest;

    private static Error Failed(string what) => EmbeddingsErrors.ServerFailed.With(what);

    private static string Status(int code) => "the embedding server answered " + code.ToString(CultureInfo.InvariantCulture);

    private static Result<T> Parse<T>(byte[] body) => AwJson.Deserialize<T>(body, OllamaWire.Options);

    private static bool IsContextLength(Reply reply)
        => reply.Status == 400
            && Parse<ErrorResponse>(reply.Body) is { IsSuccess: true } parsed
            && parsed.Value.Error?.Contains(ContextLengthMessage, StringComparison.OrdinalIgnoreCase) == true;

    private static Result<IReadOnlyList<ReadOnlyMemory<float>>> Vectors(Reply reply, int expected)
    {
        if (reply.Status != 200)
        {
            return Failed(Status(reply.Status));
        }

        var parsed = Parse<EmbedResponse>(reply.Body);
        var vectors = parsed.IsSuccess ? parsed.Value.Embeddings : null;
        return vectors is null || vectors.Length != expected || vectors.Any(v => v.Length == 0 || v.Length != vectors[0].Length)
            ? Failed("the embedding server answered an unexpected shape")
            : Results.Success<IReadOnlyList<ReadOnlyMemory<float>>>([.. vectors.Select(v => (ReadOnlyMemory<float>)v)]);
    }

    private static string FirstHalf(string text)
    {
        var cut = text.Length / 2;
        return cut > 0 && char.IsHighSurrogate(text[cut - 1]) ? text[..(cut - 1)] : text[..cut];
    }

    private Error? CheckInstalled(TagsResponse tags)
    {
        var installed = (tags.Models ?? []).FirstOrDefault(m => string.Equals(m.Name, _options.Model, StringComparison.Ordinal));
        if (installed is null)
        {
            return EmbeddingsErrors.PinMismatch.With("the embedding model is not installed; run scripts/install-deps.sh");
        }

        return SameDigest(installed.Digest, _options.Digest)
            ? null
            : EmbeddingsErrors.PinMismatch.With("the embedding model's digest is not the pinned one; run scripts/install-deps.sh");
    }

    private Error? CheckTarget()
        => !IsLoopbackUrl(_options.Url)
            ? EmbeddingsErrors.TargetRefused.With("the embedding URL must be http://127.0.0.1 or http://[::1], with no credentials")
            : _options.Model.Length == 0 || _options.Model.Contains("cloud", StringComparison.OrdinalIgnoreCase)
                ? EmbeddingsErrors.TargetRefused.With("the embedding model must be a local model, never a cloud model")
                : null;

    private async ValueTask<Result<IReadOnlyList<ReadOnlyMemory<float>>>> EmbedBatch(IReadOnlyList<string> batch, CancellationToken cancellationToken)
    {
        var reply = await Post(batch, cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        if (!IsContextLength(reply.Value))
        {
            return Vectors(reply.Value, batch.Count);
        }

        // The server does not say which input is too long: send them one at a time, halving the one that is.
        var vectors = new List<ReadOnlyMemory<float>>(batch.Count);
        foreach (var text in batch)
        {
            var one = await EmbedOne(text, cancellationToken).ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            vectors.Add(one.Value);
        }

        return vectors;
    }

    private async ValueTask<Result<ReadOnlyMemory<float>>> EmbedOne(string text, CancellationToken cancellationToken)
    {
        var reply = await Post([text], cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        if (IsContextLength(reply.Value))
        {
            var half = FirstHalf(text);
            if (half.Length == 0)
            {
                return Failed("an input is too long for the embedding model and cannot be cut further");
            }

            reply = await Post([half], cancellationToken).ConfigureAwait(false);
            if (reply.IsError)
            {
                return reply.Error;
            }

            if (IsContextLength(reply.Value))
            {
                return Failed("an input is too long for the embedding model even halved");
            }
        }

        var vectors = Vectors(reply.Value, 1);
        return vectors.IsError ? vectors.Error : vectors.Value[0];
    }

    private ValueTask<Result<Reply>> Post(IReadOnlyList<string> input, CancellationToken cancellationToken)
        => Send(HttpMethod.Post, EmbedPath, new EmbedRequest(_options.Model, input, Truncate: false), _embedTimeout, cancellationToken);

    private async ValueTask<Result<Reply>> Send(HttpMethod method, string path, EmbedRequest? body, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            using var request = new HttpRequestMessage(method, new Uri(new Uri(_options.Url), path));
            if (body is not null)
            {
                request.Content = new ByteArrayContent(AwJson.Serialize(body, OllamaWire.Options));
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            using var response = await _http.SendAsync(request, limit.Token).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(limit.Token).ConfigureAwait(false);
            return new Reply((int)response.StatusCode, bytes);
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

    private readonly record struct Reply(int Status, byte[] Body);
}
