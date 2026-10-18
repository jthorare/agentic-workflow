using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.SessionReports;

/// <summary>What the session slices need: the views harness, a fake for the launch's files, the heartbeat clock, and a launch the launcher started for the first item.</summary>
internal sealed class SessionHarness
{
    public ViewsHarness Views { get; } = new();

    public FakeLaunchFiles Files { get; } = new();

    public LaunchLiveness Liveness { get; } = new();

    public DateTimeOffset Now => Views.Slice.Clock.GetUtcNow();

    /// <summary>What the target's usage reader would say about each agent session, by session id; a session not here has no transcript.</summary>
    public Dictionary<SessionId, IReadOnlyList<ModelTokens>> Tokens { get; } = [];

    public LaunchSessionEvents Events(params AgentEntry[] catalog) => Events(Views.Slice.Held, catalog);

    /// <summary>The session events over a lease holder of the caller's choosing (an empty <see cref="HeldLease"/> is a control plane that doesn't hold the lease).</summary>
    public LaunchSessionEvents Events(HeldLease held, params AgentEntry[] catalog) =>
        new(Files, Liveness, new ReportedIdAdmission(new LaunchViews(Views.Views), new AgentCatalog(catalog.Length == 0 ? [AgentEntries.ClaudeCode, AgentEntries.Codex] : catalog), new TokensOf(Tokens)), held, Views.Slice.Store, Views.Slice.Events, Views.Slice.Clock);

    private sealed class TokensOf(Dictionary<SessionId, IReadOnlyList<ModelTokens>> used) : ILaunchUsage
    {
        public ValueTask<IReadOnlyList<ModelTokens>?> OfSession(AgentId target, SessionId session, CancellationToken cancellationToken) =>
            ValueTask.FromResult(used.TryGetValue(session, out var rows) ? rows : null);
    }

    /// <summary>Holds the lease and starts <paramref name="launch"/> of the first item on <paramref name="target"/>, open in the files.</summary>
    public async Task Started(LaunchId? launch = null, string target = "claude-code")
    {
        var id = launch ?? A.FirstLaunchId;
        if (Views.Slice.Held.Current is null)
        {
            _ = await Views.Slice.HoldLease();
            await Views.Item(repo: null);
        }

        await Views.Request(id, AgentId.From(target));
        await Views.Start(id, target: AgentId.From(target));
        Files.Open(id);
    }
}
