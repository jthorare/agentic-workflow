using Aw.Sindri.Core.Sandbox.Denials;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// Reads a session's denials at its end (sandbox spec §3.6, SB-5a): the CLI's own refusals and failed paths from the transcript tail, and the hooks that
/// failed open from <c lang="text">failopen.log</c>, into the launch's counter, which is then written and published, so the events precede 'session.ended'.
/// The egress proxy counts network denials into the same counter as they happen, so <c lang="text">egress.log</c> is not read again here.
/// </summary>
internal static class DenialCollector
{
    /// <summary>Collects one launch's denials. A missing or unreadable file contributes nothing; a ledger that refuses an event leaves it queued.</summary>
    /// <param name="sources">What to read.</param>
    /// <param name="counter">The launch's counter.</param>
    /// <param name="cancellationToken">Cancels the publish.</param>
    /// <returns>A task that ends when the counter is written and published.</returns>
    internal static async ValueTask Collect(DenialSources sources, DenialCounter counter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(counter);
        if (sources.TranscriptPath is { } transcript)
        {
            var lines = TranscriptTail.Read(transcript);
            var read = sources.Kind == TranscriptKind.Codex ? CodexTranscriptDenials.Read(lines) : ClaudeTranscriptDenials.Read(lines);
            foreach (var observation in read.Observations)
            {
                counter.Record(observation);
            }

            foreach (var path in read.FailedPaths)
            {
                if (PathDenials.Match(sources.Plan, path, sources.Home) is { } denial)
                {
                    counter.Record(denial);
                }
            }
        }

        if (sources.FailOpenLogPath is { } failOpen)
        {
            foreach (var observation in FailOpenLog.Read(TranscriptTail.Read(failOpen)))
            {
                counter.Record(observation);
            }
        }

        counter.Flush();
        await counter.Publish(cancellationToken).ConfigureAwait(false);
    }
}
