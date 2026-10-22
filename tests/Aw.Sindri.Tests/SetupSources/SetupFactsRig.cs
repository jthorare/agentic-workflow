using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.SetupSources;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApproveRecipe;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.SetupSources;

/// <summary>The setup list's sources over in-memory ports: the profile, Claude Code's config, the engine's map status, the jobs view, the recipe registry and the Onyx rig.</summary>
internal sealed class SetupFactsRig : IAsyncDisposable
{
    internal const string Checkout = "/work/demo";

    private readonly AsyncServiceScope _scope;
    private int _jobs;

    internal SetupFactsRig(FakeProviderHost? host = null)
    {
        Host = host ?? new FakeProviderHost().WithoutProvider(Core.Mcp.ProviderFile.Codex).WithoutProvider(Core.Mcp.ProviderFile.Cursor);
        Engine = MapEngineProxy.Create();
        Registry = new RegistryHarness();
        _scope = Registry.Services.CreateAsyncScope();
        Facts = new SetupFacts(
            new ProfileSetupReader(Profiles, Profiles, new Core.Setup.RepoGitCache(Inspector, Clock)),
            _scope.ServiceProvider.GetRequiredService<Core.Workflow.Registry.RecipeRegistryReader>(),
            new OnyxSetupReader(new OnyxStatusReader(Onyx.Pins, new OnyxFacts(Onyx.Runtime, Onyx.Probes, new FakeSindriHome(OnyxUpRig.Home)), Onyx.Health, Onyx.Workspace), Slice.Store, Slice.Events, Jobs),
            new McpSetupReader(Profiles, Host, Jobs, Queue),
            new MapSetupReader(Profiles, Engine.Proxy, Jobs));
    }

    internal SliceHarness Slice { get; } = new();

    internal InMemoryProfiles Profiles { get; } = new();

    internal FakeRepoInspector Inspector { get; } = new();

    internal Microsoft.Extensions.Time.Testing.FakeTimeProvider Clock { get; } = new(Kernel.Testing.KnownInstants.Epoch);

    internal FakeProviderHost Host { get; }

    internal InMemoryJobViews Jobs { get; } = new();

    internal Features.SetupWatch.SetupJobQueue Queue { get; } = new(() => 0);

    internal MapEngineProxy Engine { get; }

    internal RegistryHarness Registry { get; }

    internal OnyxUpRig Onyx { get; } = new();

    internal SetupFacts Facts { get; }

    /// <summary>Records the owner's choice about Onyx ('onyx.deferred') as the ledger holds it.</summary>
    /// <param name="deferred">The choice.</param>
    /// <returns>A task.</returns>
    internal async Task Choose(bool deferred)
    {
        var grant = Slice.Held.Current!;
        var id = Slice.Ids.New<CommandId>();
        _ = await Slice.Store.Append(Slice.Events.Entry(Core.Events.EventTypes.OnyxDeferred, new Core.Events.OnyxDeferred(id, deferred), "onyx.deferred:" + id.Value), grant, Token);
    }

    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A checked profile naming one repo, demo, at the checkout constant.</summary>
    /// <param name="repos">How many repos (the first is demo; more are demo2, demo3).</param>
    /// <returns>The loaded profile.</returns>
    internal static LoadedProfile ProfileWithRepos(int repos = 1)
    {
        RepoEntry[] entries = [.. Enumerable.Range(1, repos).Select(static i => new RepoEntry(i == 1 ? "demo" : "demo" + i.ToString(CultureInfo.InvariantCulture), i == 1 ? Checkout : "/work/demo" + i.ToString(CultureInfo.InvariantCulture)))];
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Repos: entries)).Value;
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    /// <summary>Makes <paramref name="profile"/> both the live file and the approved one.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>A task.</returns>
    internal async Task Approve(LoadedProfile profile)
    {
        Profiles.Live = profile;
        _ = await Profiles.Save(profile, Token);
    }

    /// <summary>Claude Code's .claude.json with the local-scope entry sindri for each checkout named.</summary>
    /// <param name="checkouts">The checkouts and the repo each entry names.</param>
    internal void ClaudeHas(params (string Checkout, string Repo)[] checkouts)
    {
        var projects = checkouts.ToDictionary(
            static c => c.Checkout,
            static c => new { mcpServers = new { sindri = new { type = "http", url = "http://127.0.0.1:7480/sindri/mcp", headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["X-Sindri-Repo"] = c.Repo } } } },
            StringComparer.Ordinal);
        Host.Files[Host.UserConfigPath(Core.Mcp.ProviderFile.Claude)] = JsonSerializer.Serialize(new { projects });
    }

    /// <summary>Puts a build job into the jobs view.</summary>
    /// <param name="state">Its state.</param>
    /// <param name="lines">Its progress lines.</param>
    /// <param name="code">The error code of a failed one.</param>
    internal void BuildJob(JobState state, string[] lines, string? code = null)
    {
        _jobs++;
        var at = new DateTimeOffset(2026, 10, 19, 9, _jobs, 0, TimeSpan.Zero);
        Jobs.Put(new JobRow(JobId.From($"JOB-{_jobs:x32}"), JobKind.From("BuildContextMap"), JobKey.From("build"), Item: null, Repo: null, Service: null, state,
            "human:cli", at, state == JobState.Running ? null : at.AddMinutes(1), [.. lines.Select((l, i) => new JobLine(i + 1, l, at))], lines.Length, Summary: null, code, Result: null, CancelledBy: null, Sequence: _jobs));
    }

    /// <summary>Puts a RegisterMcp job for <paramref name="repo"/> into the jobs view.</summary>
    /// <param name="repo">The repo it registers.</param>
    /// <param name="state">Its state.</param>
    /// <param name="summary">How a failed one ended.</param>
    internal void RegisterJob(string repo, JobState state, string? summary = null)
    {
        _jobs++;
        var at = new DateTimeOffset(2026, 10, 19, 9, _jobs, 0, TimeSpan.Zero);
        Jobs.Put(new JobRow(JobId.From($"JOB-{_jobs:x32}"), JobKind.From("RegisterMcp"), Core.Setup.McpJobKeys.For(RepoName.From(repo)), Item: null, RepoName.From(repo), Service: null, state,
            "human:cli", at, state == JobState.Running ? null : at.AddMinutes(1), [], 0, summary, Code: null, Result: null, CancelledBy: null, Sequence: _jobs));
    }

    /// <summary>The map status the engine answers once a map is current.</summary>
    /// <returns>A status.</returns>
    internal static MapStatusView CurrentMap() =>
        new(new MapVersion(1, ContextRepoName.From("demo"), Commit: null, KeyVersion: 1, new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero), MapVersionStatus.Current), new MapCounts(10, 5, 2, 1, 0), Orphaned: 0, Retained: 0, ScopedTo: null);

    public async ValueTask DisposeAsync()
    {
        Queue.Dispose();
        await _scope.DisposeAsync();
        await Registry.DisposeAsync();
    }
}
