using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host.Commands.SessionWrap;

namespace Aw.Sindri.Tests.SessionWrap;

internal sealed class FakeIds : ISessionIdSource
{
    private int _looks;

    public Func<int, SessionIdScan> Script { get; set; } = static _ => SessionIdScan.None;

    public int Looks => Volatile.Read(ref _looks);

    public ValueTask<SessionIdScan> Look(LaunchId launch, DateTimeOffset startedAt, TimeSpan elapsed, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Script(Interlocked.Increment(ref _looks)));
}
