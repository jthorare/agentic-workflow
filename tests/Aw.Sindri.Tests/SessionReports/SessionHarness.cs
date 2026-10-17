using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
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

    public LaunchSessionEvents Events(params AgentEntry[] catalog) =>
        new(Files, Liveness, new ReportedIdAdmission(new LaunchViews(Views.Views), new AgentCatalog(catalog.Length == 0 ? [AgentEntries.ClaudeCode, AgentEntries.Codex] : catalog)), Views.Slice.Held, Views.Slice.Store, Views.Slice.Events, Views.Slice.Clock);

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
