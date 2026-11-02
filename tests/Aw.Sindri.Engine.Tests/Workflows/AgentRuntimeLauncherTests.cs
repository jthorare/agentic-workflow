using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Launch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The agent runtime over the REAL launcher (engine P8 U8, launcher spec SL-5): the engine's launch activity asks the composed host's launcher for an unattended
/// Step, and a stub agent runs in a tmux session on a private socket (never the team's). Nothing sleeps on the wall clock: the host answers when its session is up.
/// </summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class AgentRuntimeLauncherTests : IDisposable
{
    private const string Repo = "app";
    private const string Item = "ITM-00000000000000000000000000000009";
    private readonly WorkflowsHarness _harness = new();
    private readonly PrivateTmux _tmux = new();
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-sl5-").FullName;

    public void Dispose()
    {
        _tmux.Dispose();
        _harness.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private string Checkout => Path.Combine(_dir, "checkout");

    private string LauncherJson => Path.Combine(_dir, "launcher.json");

    [Fact(Timeout = 120_000)]
    public async Task A_host_none_launcher_json_still_starts_the_step_in_tmux()
    {
        var ct = TestContext.Current.CancellationToken;
        await InitRepo(ct);
        WriteLauncher(host: "none", claudeBin: Stub("claude"));
        await using var rig = await Rig("claude-code", agents: null, ct);

        var launched = await rig.Launch(Run(1), ct);

        Assert.Null(launched.Failure);
        Assert.True(_tmux.HasSession(LaunchSessionName.For(AgentLaunches.LaunchIdFor(Run(1)))));
    }

    [Fact(Timeout = 120_000)]
    public async Task An_added_entry_without_step_argv_hash_parks_not_approved()
    {
        var ct = TestContext.Current.CancellationToken;
        await InitRepo(ct);
        WriteLauncher(aiderOpen: ["{bin}", "--message", "{seed}"]);
        await using var rig = await Rig("aider", agents: null, ct);

        var launched = await rig.Launch(Run(1), ct);

        Assert.Equal((AgentLaunchFailureKind.Park, "SND-LAUNCH-012"), (launched.Failure?.Kind, launched.Failure?.Code));
        Assert.Contains("not-approved", launched.Failure?.Message, StringComparison.Ordinal);
        Assert.False(_tmux.HasSession(LaunchSessionName.For(AgentLaunches.LaunchIdFor(Run(1)))));
    }

    [Fact(Timeout = 120_000)]
    public async Task With_the_hash_approved_it_launches()
    {
        var ct = TestContext.Current.CancellationToken;
        await InitRepo(ct);
        WriteLauncher(aiderOpen: ["{bin}", "--message", "{seed}"]);
        var hash = HashOf("aider");
        await using var rig = await Rig("aider", Approved("aider", hash), ct);

        var launched = await rig.Launch(Run(1), ct);

        Assert.Null(launched.Failure);
        Assert.True(_tmux.HasSession(LaunchSessionName.For(AgentLaunches.LaunchIdFor(Run(1)))));
    }

    [Fact(Timeout = 120_000)]
    public async Task A_changed_template_parks_hash_changed_with_both_hashes()
    {
        var ct = TestContext.Current.CancellationToken;
        await InitRepo(ct);
        WriteLauncher(aiderOpen: ["{bin}", "--message", "{seed}"]);
        var approved = HashOf("aider");
        WriteLauncher(aiderOpen: ["{bin}", "--yes", "--message", "{seed}"]);   // the entry's open template changed after the approval
        var current = HashOf("aider");
        await using var rig = await Rig("aider", Approved("aider", approved), ct);

        var launched = await rig.Launch(Run(1), ct);

        Assert.Equal((AgentLaunchFailureKind.Park, "SND-LAUNCH-012"), (launched.Failure?.Kind, launched.Failure?.Code));
        Assert.Contains("hash-changed", launched.Failure?.Message, StringComparison.Ordinal);
        var failed = await rig.Payload("launch.failed", ct);
        Assert.Contains(approved, failed, StringComparison.Ordinal);
        Assert.Contains(current, failed, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_bin_only_override_launches_unapproved()
    {
        var ct = TestContext.Current.CancellationToken;
        await InitRepo(ct);
        WriteLauncher(claudeBin: Stub("claude"));
        await using var rig = await Rig("claude-code", agents: null, ct);

        var launched = await rig.Launch(Run(1), ct);

        Assert.Null(launched.Failure);
        Assert.True(_tmux.HasSession(LaunchSessionName.For(AgentLaunches.LaunchIdFor(Run(1)))));
    }

    /// <summary>I28: after a restart the same run asks again for a session that is still live. The new launcher answers with the live session and starts no second one.</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_restart_relaunch_of_a_live_session_stays_on_it()
    {
        var ct = TestContext.Current.CancellationToken;
        await InitRepo(ct);
        WriteLauncher(claudeBin: Stub("claude"));
        var name = LaunchSessionName.For(AgentLaunches.LaunchIdFor(Run(1)));
        await using (var first = await Rig("claude-code", agents: null, ct))
        {
            Assert.Null((await first.Launch(Run(1), ct)).Failure);
        }

        await using var second = await Rig("claude-code", agents: null, ct, restart: true);   // a new process on the same home: its launcher remembers nothing
        var request = AgentLaunches.RequestFor(Run(1), new AgentStepTarget(ItemId.From(Item), AgentId.From("claude-code"), Checkout));
        var again = await second.Relaunch(request, ct);

        var started = Assert.IsType<StartedLaunch>(again);
        Assert.Equal((ViewerOutcomeKind.Skipped, name), (started.Viewer.Kind, started.Host.Name));
        Assert.Equal([name], _tmux.Run("list-sessions", "-F", "#{session_name}")!.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    private static AgentStepRun Run(int attempt)
    {
        var address = InstanceAddress.From("T-9/step/" + attempt.ToString(CultureInfo.InvariantCulture));
        return new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.From(attempt.ToString(CultureInfo.InvariantCulture) + ".1"), new StepRef("write", "step.produce"), WriteScope: false);
    }

    private static AgentSettings Approved(string agent, string hash) => new(Targets: new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { [agent] = new(StepArgvHash: [hash]) });

    private async Task<LauncherRig> Rig(string target, AgentSettings? agents, CancellationToken ct, bool restart = false)
    {
        var sindri = _harness.Sindri(
            services =>
            {
                _ = services.Replace(ServiceDescriptor.Singleton(new TmuxSessionHost(_tmux.Options)));
                _ = services.RemoveAll<ISandboxLaunch>();   // these pin the launcher and tmux, not the sandbox (SandboxStepProofTests and the heavy lane do): Steps run unconfined here
            },
            Variable);
        if (restart)
        {
            return new LauncherRig(sindri, item: null);   // the first process's lease, approved profile and item fact are still in the home
        }

        await WorkflowsHarness.HoldLease(sindri, ct);
        var declared = new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester", Repos: [new RepoEntry(Repo, Checkout)], Agents: agents,
            Workflow: new WorkflowSettings(Agent: new WorkflowAgentSettings(AgentId.From(target))));
        var profile = ProfileRules.Check(declared).Value;
        var saved = await sindri.GetRequiredService<IApprovals>().Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), ct);
        Assert.False(saved.IsError, saved.IsError ? saved.Error.Message : null);
        var item = _harness.Fact(JsonSerializer.Serialize(new { id = Item, repo = Repo, spec = (string?)null }));
        return new LauncherRig(sindri, item);
    }

    private string? Variable(string name) => name switch
    {
        "AW_LAUNCHER_CONFIG" => LauncherJson,
        "AW_SINDRI_EXE" => Path.Combine(_dir, "sindri"),
        _ => null,
    };

    private string HashOf(string agent)
    {
        var catalog = new ReloadingAgentCatalog(LauncherJson, LauncherStartup.Builtins());
        return catalog.Resolve(AgentId.From(agent))?.ArgvHash ?? throw new InvalidOperationException($"{agent} is not in the catalog: {catalog.Current.Problem}");
    }

    /// <summary>A stub agent CLI: records its arguments and stays alive, so the session is observable.</summary>
    private string Stub(string name)
    {
        var path = Path.Combine(_dir, name);
        return ExecutableScript.Write(path, $"echo \"$@\" > \"{_dir}/$AW_SINDRI_LAUNCH.args\"\nexec sleep 300\n");
    }

    private void WriteLauncher(string? host = null, string? claudeBin = null, string[]? aiderOpen = null)
    {
        var agents = new Dictionary<string, object>(StringComparer.Ordinal);
        if (claudeBin is not null)
        {
            agents["claude-code"] = new { @override = true, bin = claudeBin };
        }

        if (aiderOpen is not null)
        {
            agents["aider"] = new { label = "Aider", bin = Stub("aider"), open = aiderOpen, sessionId = "none", policy = "claude-settings-file" };
        }

        var wrap = Path.Combine(_dir, "sindri");   // 'session wrap --launch <id> -- <argv...>' without the wrap: run the agent
        _ = ExecutableScript.Write(wrap, "while [ \"$1\" != \"--\" ]; do shift; done\nshift\nexec \"$@\"\n");
        var document = new Dictionary<string, object>(StringComparer.Ordinal) { ["schema"] = 1, ["agents"] = agents };
        if (host is not null)
        {
            document["host"] = new { kind = host };
        }

        File.WriteAllText(LauncherJson, JsonSerializer.Serialize(document));
    }

    private async Task InitRepo(CancellationToken ct)
    {
        // Independent of the box's git config: an empty template copies no hooks (the box's init.templateDir installs the sindri pre-commit hook, which runs a scrub that can overrun the probe budget under load).
        _ = Directory.CreateDirectory(Checkout);
        Assert.NotNull(await ViewerProcess.Output("git", ["-C", Checkout, .. IsolatedGit.Init()], ct));
        Assert.NotNull(await ViewerProcess.Output("git", ["-C", Checkout, .. IsolatedGit.NoHooks, "-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "--allow-empty", "-m", "init"], ct));
    }

    private sealed class LauncherRig(ServiceProvider sindri, FactValue? item) : IAsyncDisposable
    {
        public Task<AgentLaunched> Launch(AgentStepRun run, CancellationToken ct) =>
            sindri.GetRequiredService<AgentLaunching>().Launch(new AgentLaunchRun(run, item), ct);

        public async Task<LaunchResult> Relaunch(LaunchRequest request, CancellationToken ct) =>
            await sindri.GetRequiredService<ILauncher>().Launch(request, ct);

        /// <summary>The payload text of the first ledger row of <paramref name="type"/>.</summary>
        public async Task<string> Payload(string type, CancellationToken ct)
        {
            var rows = (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 200, ct)).Value;
            return rows.First(e => e.Envelope.Type == type).Envelope.Payload;
        }

        public ValueTask DisposeAsync() => sindri.DisposeAsync();
    }
}
