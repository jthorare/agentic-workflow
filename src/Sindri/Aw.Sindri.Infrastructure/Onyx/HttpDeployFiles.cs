using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>
/// IDeployFiles over HTTPS from raw.githubusercontent.com at the pinned commit. https only (plain http only for a loopback base, which a test serves),
/// no redirect is followed, 30 s per file, 1 MiB per file, and nothing is written until the sha256 matches the lock: a file goes to a temporary
/// name in its own directory and is renamed over the target, so a failed or interrupted fetch leaves what was there.
/// </summary>
internal sealed class HttpDeployFiles : IDeployFiles
{
    private const int MaxBytes = 1 << 20;

    private readonly HttpClient _client;
    private readonly Uri _baseUri;
    private readonly TimeSpan _fileTimeout;

    /// <summary>Initializes the adapter for the production timeout.</summary>
    /// <param name="client">The client; it must not follow redirects (a followed redirect is refused anyway).</param>
    /// <param name="baseUri">The address ending in the repository, such as https://raw.githubusercontent.com/onyx-dot-app/onyx/.</param>
    public HttpDeployFiles(HttpClient client, Uri baseUri)
        : this(client, baseUri, TimeSpan.FromSeconds(30))
    {
    }

    /// <summary>Initializes the adapter with a per-file timeout (a test sets a short one).</summary>
    /// <param name="client">The client.</param>
    /// <param name="baseUri">The base address: https, or http when it is a loopback address.</param>
    /// <param name="fileTimeout">How long one file may take.</param>
    /// <exception cref="ArgumentException">The base is neither https nor a loopback address: Composition picks it, so this is a programming error, not input.</exception>
    internal HttpDeployFiles(HttpClient client, Uri baseUri, TimeSpan fileTimeout)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(baseUri);
        if (baseUri.Scheme != Uri.UriSchemeHttps && !(baseUri.Scheme == Uri.UriSchemeHttp && baseUri.IsLoopback))
        {
            throw new ArgumentException($"the deployment files come over https only (or http from a loopback address), not from {baseUri.Scheme}://{baseUri.Host}", nameof(baseUri));
        }

        (_client, _baseUri, _fileTimeout) = (client, baseUri, fileTimeout);
    }

    /// <inheritdoc />
    public async ValueTask<Result<DeployFetch>> Fetch(OnyxPins pins, string deployDir, Action<string> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pins);
        Error? first = null;
        int downloaded = 0, present = 0;
        foreach (var pin in pins.Files)
        {
            var target = Path.Combine(deployDir, pin.Path);
            if (File.Exists(target) && pin.Matches(await File.ReadAllBytesAsync(target, cancellationToken).ConfigureAwait(false)))
            {
                present++;
                progress($"present {pin.Path}");
                continue;
            }

            var one = await FetchOne(pins.Commit, pin, target, cancellationToken).ConfigureAwait(false);
            if (one is null)
            {
                downloaded++;
                progress($"fetched {pin.Path}");
            }
            else
            {
                first ??= one;
            }
        }

        return first is { } error ? error : new DeployFetch(downloaded, present);
    }

    // Null when the file was fetched, verified and written; otherwise the error, with nothing written.
    private async Task<Error?> FetchOne(string commit, OnyxFilePin pin, string target, CancellationToken cancellationToken)
    {
        var read = await Download(new Uri(_baseUri, $"{commit}/deployment/{pin.Path}"), pin, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        if (!pin.Matches(read.Value))
        {
            return pin.Mismatch(read.Value);
        }

        _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);   // why: the target always has a directory
        var temporary = target + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(temporary, read.Value, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }

        return null;
    }

    private async Task<Result<byte[]>> Download(Uri uri, OnyxFilePin pin, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_fileTimeout);
        try
        {
            using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400 || response.RequestMessage?.RequestUri != uri)
            {
                return pin.Unavailable("the host answered with a redirect, which Sindri never follows");
            }

            if (!response.IsSuccessStatusCode)
            {
                return pin.Unavailable($"HTTP {((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)}");
            }

            return await ReadCapped(response, pin, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return pin.Unavailable($"timed out after {_fileTimeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} s");
        }
        catch (HttpRequestException exception)
        {
            return pin.Unavailable($"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static async Task<Result<byte[]>> ReadCapped(HttpResponseMessage response, OnyxFilePin pin, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaxBytes)
        {
            return pin.Unavailable("the body is over 1 MiB");
        }

        var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (body.ConfigureAwait(false))
        {
            return await Drain(body, pin, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<Result<byte[]>> Drain(Stream body, OnyxFilePin pin, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int count;
        while ((count = await body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            if (buffer.Length > MaxBytes)
            {
                return pin.Unavailable("the body is over 1 MiB");
            }
        }

        return buffer.ToArray();
    }
}
