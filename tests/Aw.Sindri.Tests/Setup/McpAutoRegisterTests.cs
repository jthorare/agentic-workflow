using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.RegisterMcp;
using Aw.Sindri.Features.SetupWatch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Setup;

/// <summary>
/// The automatic runs of the setup service's mcp step (setup-once spec §5.4 (a) and (b)): they read the approved snapshot only, start nothing before consent is recorded, stay off for the profile key and the environment switch,
/// leave a repo whose newest job failed to the owner, and start nothing for a registration that is already there.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class McpAutoRegisterTests : IDisposable
{
    private readonly SliceHarness _slice = new();
    private readonly FakeProviderHost _host = new FakeProviderHost().WithoutProvider(Core.Mcp.ProviderFile.Codex).WithoutProvider(Core.Mcp.ProviderFile.Claude);
    private readonly InMemoryJobViews _jobs = new();
    private readonly Starter _starter = new();
    private readonly Switch _switch = new();
    private readonly InMemoryEndpointFile _endpoint = new();
    private readonly McpAutoMemory _memory = new();
    private readonly SetupJobQueue _queue = new(() => 0);   // one queue for the test, as serve has one: a job left waiting starts on a later tick

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _queue.Dispose();

    [Fact]
    public async Task With_consent_a_repo_that_is_not_registered_gets_one_job_through_the_queue()
    {
        var approved = await Approve(repos: ["demo"]);
        await Consent(approved);

        await Run();

        Assert.Equal(["demo"], _starter.Repos);
    }

    [Fact]
    public async Task Without_consent_nothing_is_started_and_the_owner_is_asked_once()
    {
        var approved = await Approve(repos: ["demo"]);

        await Run();

        Assert.Empty(_starter.Repos);
        Assert.True(await Gate().TurnOnNeeded(Token));
        await Consent(approved);
        Assert.False(await Gate().TurnOnNeeded(Token));
    }

    [Fact]
    public async Task The_profile_key_setup_mcp_off_starts_nothing_and_asks_nothing()
    {
        var approved = await Approve(repos: ["demo"], mcp: SetupSettings.Off);
        await Consent(approved);

        await Run();

        Assert.Empty(_starter.Repos);
        Assert.False(await Gate().TurnOnNeeded(Token));
    }

    [Fact]
    public async Task The_environment_switch_starts_nothing_and_asks_nothing()
    {
        var approved = await Approve(repos: ["demo"]);
        await Consent(approved);
        _switch.Off = true;

        await Run();

        Assert.Empty(_starter.Repos);
        Assert.False(await Gate().TurnOnNeeded(Token));
    }

    [Fact]
    public async Task A_repo_only_in_a_drifted_live_profile_is_never_started()
    {
        var approved = await Approve(repos: ["demo"]);
        await Consent(approved);
        var live = SliceHarness.Profile();
        _slice.Profiles.Live = live with { Profile = live.Profile with { Repos = [new RepoEntry("demo", "/work/demo"), new RepoEntry("drifted", "/work/drifted")] } };

        await Run();

        Assert.Equal(["demo"], _starter.Repos);
    }

    [Fact]
    public async Task A_registration_that_needs_no_change_starts_nothing()
    {
        var approved = await Approve(repos: ["demo"]);
        await Consent(approved);
        _ = _host.WithoutProvider(Core.Mcp.ProviderFile.Cursor);   // no provider is installed: every outcome is skipped

        await Run();

        Assert.Empty(_starter.Repos);
    }

    [Fact]
    public async Task A_repo_whose_newest_job_failed_is_left_to_the_owner()
    {
        var approved = await Approve(repos: ["demo"]);
        await Consent(approved);
        _jobs.Put(new JobRow(JobId.From("JOB-" + 1.ToString("x32", System.Globalization.CultureInfo.InvariantCulture)), JobKind.From("RegisterMcp"), JobKey.From("mcp-a"), Item: null, RepoName.From("demo"), Service: null, JobState.Failed, "service:setup",
            KnownInstants.Epoch, KnownInstants.Epoch.AddMinutes(1), [], 0, "failed", "SND-SVC-001", Result: null, CancelledBy: null, Sequence: 1));

        await Run();

        Assert.Empty(_starter.Repos);
    }

    [Fact]
    public async Task A_checkout_that_tracks_a_project_file_still_gets_the_job_so_its_row_can_name_the_file()
    {
        var approved = await Approve(repos: ["demo"]);
        await Consent(approved);
        _host.Files["/work/demo/.cursor/mcp.json"] = "{}";
        _ = _host.WithTracked("/work/demo/.cursor/mcp.json");

        await Run();

        Assert.Equal(["demo"], _starter.Repos);
    }

    [Fact]
    public async Task Ticks_with_nothing_changed_start_one_job_and_a_port_change_starts_a_second()
    {
        var approved = await Approve(repos: ["demo"]);
        await Consent(approved);
        _ = await Listen(7480);

        await Run();
        await Run();
        await Run();

        Assert.Equal(["demo"], _starter.Repos);
        _ = await Listen(7481);
        await Run();
        await Run();
        Assert.Equal(["demo", "demo"], _starter.Repos);
    }

    [Fact]
    public async Task A_new_approved_profile_that_names_the_repo_starts_a_job_again()
    {
        await Consent(await Approve(repos: ["demo"]));
        await Run();

        await Consent(await Approve(repos: ["demo", "other"]));
        await Run();
        await Run();

        Assert.Equal(["demo", "demo", "other"], _starter.Repos);
    }

    [Fact]
    public async Task A_new_serve_process_looks_again()
    {
        await Consent(await Approve(repos: ["demo"]));
        await Run();

        await Run(new McpAutoMemory());

        Assert.Equal(["demo", "demo"], _starter.Repos);
    }

    [Fact]
    public async Task Only_a_humans_press_records_the_consent()
    {
        var approved = await Approve(repos: ["demo"]);
        using (var session = _slice.Callers.Enter(A.Caller().Session().Build()))
        {
            _ = await Events().ConsentOnHumanPress(approved.Hash, Token);
        }

        Assert.False(await Events().ConsentGiven(approved.Hash, Token));
        await Consent(approved);
        Assert.True(await Events().ConsentGiven(approved.Hash, Token));
    }

    private McpAutoGate Gate() => new(_slice.Profiles, Events(), _switch, _jobs);

    private McpEvents Events() => new(_slice.Held, _slice.Store, _slice.Events, _slice.Callers);

    private Task<Result<Unit>> Listen(int port) => _endpoint.Write(new ControlEndpoint(new Uri("http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture)), Pid: 1, Epoch: 1), Token).AsTask();

    private Task Run() => Run(_memory);

    private async Task Run(McpAutoMemory memory)
    {
        var need = new McpNeed(new AutomaticMcpRegistration(new RegisterMcpService(_slice.Profiles, new FakeRepoLocator(), _endpoint, new FakeClaudeCli(), _host) { WriteCodexTrust = false }));
        var changed = new ChangedMcpNeed(need, _slice.Profiles, _endpoint, memory);
        await new McpAutoRegister(Gate(), changed, _queue, _starter, new NothingRunning()).Run(Token);
    }

    private async Task<LoadedProfile> Approve(string[] repos, string? mcp = null)
    {
        if (_slice.Held.Current is null)   // a second approval in one test keeps the lease the first took
        {
            _ = await _slice.HoldLease();
        }
        var live = SliceHarness.Profile();
        var profile = live.Profile with { Repos = [.. repos.Select(static r => new RepoEntry(r, "/work/" + r))], Setup = mcp is null ? null : new SetupSettings(mcp) };
        var loaded = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), live.Source);   // a new profile gets its own hash
        _ = await _slice.Profiles.Save(loaded, Token);
        return loaded;
    }

    private async Task Consent(LoadedProfile approved)
    {
        using var human = _slice.Callers.Enter(A.Caller().Human().Client(ClientLabel.Ui).Build());
        _ = await Events().ConsentOnHumanPress(approved.Hash, Token);
    }

    private sealed class Starter : IMcpJobStarter
    {
        public List<string> Repos { get; } = [];

        public ValueTask<Result<Unit>> Start(RepoName repo, CancellationToken cancellationToken)
        {
            Repos.Add(repo.Value);
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }

    private sealed class Switch : IMcpAutoSwitch
    {
        public bool Off { get; set; }
    }

    private sealed class NothingRunning : IRunningJobs
    {
        private static readonly JobId _none = JobId.From("JOB-" + new string('0', 32));

        public bool IsRunning(JobKind kind, JobKey key, out JobId running)
        {
            running = _none;
            return false;
        }
    }
}
