using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Tests.SessionReports;

/// <summary>A usage sampler that writes nothing and remembers how many ledger events existed at each flush, so a test can tell what the flush came before.</summary>
/// <param name="harness">The session harness whose ledger is watched.</param>
internal sealed class RecordingUsageSampler(SessionHarness harness) : ISessionUsageSampler
{
    private readonly List<(LaunchId Launch, int EventsBefore)> _flushes = [];

    /// <summary>Each flush: the launch and how many ledger events there were when it was asked.</summary>
    public IReadOnlyList<(LaunchId Launch, int EventsBefore)> Flushes => _flushes;

    public ValueTask<int> Sample(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<int> Flush(LaunchId launch, CancellationToken cancellationToken)
    {
        _flushes.Add((launch, harness.Views.Slice.Store.Events.Count));
        return ValueTask.FromResult(0);
    }
}
