using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Tests.SessionReports;

namespace Aw.Sindri.Tests.SessionExit;

/// <summary>Records each flush with how many 'session.ended' events the ledger held when it ran.</summary>
internal sealed class RecordingUsageSampler(SessionHarness harness) : ISessionUsageSampler
{
    public List<(LaunchId Launch, int EndedBefore)> Flushed { get; } = [];

    public bool Fail { get; set; }

    public ValueTask<int> Sample(CancellationToken cancellationToken) => ValueTask.FromResult(0);

    public ValueTask<int> Flush(LaunchId launch, CancellationToken cancellationToken)
    {
        Flushed.Add((launch, harness.Views.Slice.Store.Events.Count(static e => string.Equals(e.Envelope.Type, EventTypes.SessionEnded, StringComparison.Ordinal))));
        return Fail ? throw new InvalidOperationException("the transcript is unreadable") : ValueTask.FromResult(1);
    }
}
