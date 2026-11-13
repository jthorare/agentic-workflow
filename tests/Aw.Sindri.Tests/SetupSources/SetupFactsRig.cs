using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.SetupSources;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.SetupSources;

/// <summary>The setup list's sources over in-memory ports: the profile, Claude Code's config, the engine's map status, the jobs view, the recipe registry and the token pointers.</summary>
internal sealed class SetupFactsRig : IAsyncDisposable
{
    internal const string Checkout = "/work/demo";

    private int _jobs;

    internal SetupFactsRig(FakeProviderHost? host = null, Core.Scheduling.IServicePauses? pauses = null)
    {
        Pauses = pauses ?? new Server.NoPauses();
        Host = host ?? new FakeProviderHost().WithoutProvider(Core.Mcp.ProviderFile.Codex).WithoutProvider(Core.Mcp.ProviderFile.Cursor);
        Engine = MapEngineProxy.Create();
        Facts = new SetupFacts(
            new ProfileSetupReader(Profiles, new Core.Setup.RepoGitCache(Inspector, Clock), new NoProfileFile(), new Testing.Workflow.InMemoryWorkflowStore(Clock), new Testing.Workflow.InMemoryWorkflowRunStore(Clock)),
            Sources(new PointerSecrets(name => Env.GetValueOrDefault(name), new FakeToolRunner(ToolRun.Missing))),
            new McpSetupReader(Profiles, Host, Jobs, Queue),
            new MapSetupReader(Profiles, Engine.Proxy, Jobs));
    }

    /// <summary>The Connections page's rows the sources step asks first (#2508).</summary>
    internal InMemoryConnectionStore Connections { get; } = new();

    /// <summary>The connections' tokens.</summary>
    internal FakeConnectionSecrets ConnectionSecrets { get; } = new();

    /// <summary>The environment variables the token pointers (env:NAME) resolve against.</summary>
    internal Dictionary<string, string> Env { get; } = new(StringComparer.Ordinal);

    internal SliceHarness Slice { get; } = new();

    /// <summary>Adds a connection of the preset with a stored token.</summary>
    /// <param name="preset">The preset ('github', 'slack', 'linear').</param>
    /// <param name="status">Its status.</param>
    /// <returns>A task.</returns>
    internal async Task Connect(string preset, Core.Connections.ConnectionStatus status)
    {
        var connection = ConnectionSamples.Oauth(ConnectionSamples.Name(preset, "work"), status);
        _ = (await Connections.Insert(connection, Token)).Value;
        _ = ConnectionSecrets.Store(connection.Name, new Core.Connections.TokenSet(preset + "-connection-token", RefreshToken: null, ExpiresAt: null)).Value;
    }

    private SourcesSetupReader Sources(PointerSecrets secrets) => new(Profiles, FakeContextEngine.Instance, secrets, new Core.Connections.ConnectionCredentials(Connections, ConnectionSecrets, secrets));

    /// <summary>The services a human paused (none unless the test names them).</summary>
    internal Core.Scheduling.IServicePauses Pauses { get; }

    internal InMemoryProfiles Profiles { get; } = new();

    internal FakeRepoInspector Inspector { get; } = new();

    internal Microsoft.Extensions.Time.Testing.FakeTimeProvider Clock { get; } = new(Kernel.Testing.KnownInstants.Epoch);

    internal FakeProviderHost Host { get; }

    internal InMemoryJobViews Jobs { get; } = new();

    internal Features.SetupWatch.SetupJobQueue Queue { get; } = new(() => 0);

    internal MapEngineProxy Engine { get; }

    internal SetupFacts Facts { get; }

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

    /// <summary>A checked profile naming one repo and the given context section (the JSON of 'context:').</summary>
    /// <param name="context">The context section.</param>
    /// <returns>The loaded profile.</returns>
    internal static LoadedProfile ProfileWithContext(string context)
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Repos: [new RepoEntry("demo", Checkout)], Context: JsonDocument.Parse(context).RootElement.Clone())).Value;
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

    /// <summary>Puts a RefreshContextMap job (the built-in context-map-refresh service's, which builds the first map) into the jobs view.</summary>
    /// <param name="state">Its state.</param>
    /// <param name="lines">Its progress lines.</param>
    /// <param name="code">The error code of a failed one.</param>
    internal void RefreshJob(JobState state, string[] lines, string? code = null)
    {
        _jobs++;
        var at = new DateTimeOffset(2026, 10, 19, 9, _jobs, 0, TimeSpan.Zero);
        Jobs.Put(new JobRow(JobId.From($"JOB-{_jobs:x32}"), JobKind.From("RefreshContextMap"), JobKey.From("refresh"), Item: null, Repo: null, Service: null, state,
            "service:context-map-refresh", at, state == JobState.Running ? null : at.AddMinutes(1), [.. lines.Select((l, i) => new JobLine(i + 1, l, at))], lines.Length, Summary: null, code, Result: null, CancelledBy: null, Sequence: _jobs));
    }

    /// <summary>Puts a code index build job of <paramref name="repo"/> into the jobs view (kind BuildCodeIndex, key 'index:&lt;repo&gt;').</summary>
    /// <param name="repo">The repo it builds.</param>
    /// <param name="state">Its state.</param>
    /// <param name="lines">Its progress lines.</param>
    /// <param name="code">The error code of a failed one.</param>
    internal void IndexJob(string repo, JobState state, string[] lines, string? code = null)
    {
        _jobs++;
        var at = new DateTimeOffset(2026, 10, 19, 9, _jobs, 0, TimeSpan.Zero);
        Jobs.Put(new JobRow(JobId.From($"JOB-{_jobs:x32}"), JobKind.From("BuildCodeIndex"), JobKey.From("index:" + repo), Item: null, RepoName.From(repo), Service: null, state,
            "service:code-index", at, state == JobState.Running ? null : at.AddMinutes(1), [.. lines.Select((l, i) => new JobLine(i + 1, l, at))], lines.Length, Summary: null, code, Result: null, CancelledBy: null, Sequence: _jobs));
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

    public ValueTask DisposeAsync()
    {
        Queue.Dispose();
        return ValueTask.CompletedTask;
    }

    // The rig's profile source answers from memory, so there is never a file on disk.
    internal sealed class NoProfileFile : IProfileFile
    {
        public bool Exists() => false;
    }
}
