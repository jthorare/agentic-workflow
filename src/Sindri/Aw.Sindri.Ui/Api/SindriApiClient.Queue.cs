using System.Collections.Concurrent;
using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The queue read (<see cref="SindriApiClient"/>, continued): conditional on the tag of the last view, so an unchanged queue costs a 304 and keeps the view the app holds (#2862).</summary>
internal sealed partial class SindriApiClient
{
    // The last tagged queue view per API base URL: its tag and the view, which a 304 answers with.
    private readonly ConcurrentDictionary<string, (string Tag, QueueView View)> _queues = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<Result<QueueView>> Queue(Uri api, CancellationToken cancellationToken)
    {
        var key = api.AbsoluteUri;
        var (tag, kept) = _queues.TryGetValue(key, out var held) ? held : default;
        var options = new CallOptions(View: true, IfNoneMatch: tag is null ? null : "\"" + tag + "\"", Kept: kept);
        var answer = await Call<QueueView>(api, HttpMethod.Get, "/sindri/queue", cancellationToken, options).ConfigureAwait(false);
        if (answer.IsSuccess)
        {
            if (answer.Value.Tag is { Length: > 0 } fresh)
            {
                _queues[key] = (fresh, answer.Value);
            }
            else
            {
                _ = _queues.TryRemove(key, out _);
            }
        }

        return answer;   // an error keeps the old entry
    }
}
