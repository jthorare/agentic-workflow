using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Features.ScanRepos;
using Microsoft.AspNetCore.Http.Features;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The 200 answer of a scan: 'application/x-ndjson', never cached, unbuffered, one JSON line per row flushed as the walk finds it, then the closing line. When the client goes away the read is cancelled,
/// which stops the walk (first-run spec §3.3 "Cancel").
/// </summary>
/// <param name="scan">The scan.</param>
/// <param name="json">The package's AwJson options.</param>
internal sealed class RepoScanResult(ScanReposResponse scan, JsonSerializerOptions json) : IResult
{
    private static readonly byte[] _newLine = "\n"u8.ToArray();

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var response = httpContext.Response;
        response.ContentType = "application/x-ndjson";
        response.Headers.CacheControl = "no-store";
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        var gone = httpContext.RequestAborted;
        try
        {
            await response.StartAsync(gone).ConfigureAwait(false);
            await foreach (var row in scan.Rows.WithCancellation(gone).ConfigureAwait(false))
            {
                await Line(response.Body, row, gone).ConfigureAwait(false);
            }

            await Line(response.Body, scan.End(), gone).ConfigureAwait(false);
        }
        catch (Exception e) when (gone.IsCancellationRequested && e is OperationCanceledException or IOException)
        {
            // the client went away: the walk stopped with the read, and there is no one to answer
        }
    }

    private async Task Line<T>(Stream body, T value, CancellationToken cancellationToken)
    {
        await body.WriteAsync(AwJson.Serialize(value, json), cancellationToken).ConfigureAwait(false);
        await body.WriteAsync(_newLine, cancellationToken).ConfigureAwait(false);
        await body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
