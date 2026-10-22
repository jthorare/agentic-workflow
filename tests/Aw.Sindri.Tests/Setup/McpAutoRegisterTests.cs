using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
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
public sealed class McpAutoRegisterTests
{
    private readonly SliceHarness _slice = new();
    private readonly FakeProviderHost _host = new FakeProviderHost().WithoutProvider(Core.Mcp.ProviderFile.Codex).WithoutProvider(Core.Mcp.ProviderFile.Claude);
    private readonly InMemoryJobViews _jobs = new();
    private readonly Starter _starter = new();
    private readonly Switch _switch = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

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

    private async Task Run()
    {
        using var queue = new SetupJobQueue(() => 0);
        var need = new McpNeed(new AutomaticMcpRegistration(new RegisterMcpService(_slice.Profiles, new FakeRepoLocator(), new InMemoryEndpointFile(), new FakeClaudeCli(), _host) { WriteCodexTrust = false }));
        await new McpAutoRegister(Gate(), need, queue, _starter, new NothingRunning()).Run(Token);
    }

    private async Task<LoadedProfile> Approve(string[] repos, string? mcp = null)
    {
        _ = await _slice.HoldLease();
        var live = SliceHarness.Profile();
        var profile = live.Profile with { Repos = [.. repos.Select(static r => new RepoEntry(r, "/work/" + r))], Setup = mcp is null ? null : new SetupSettings(mcp) };
        var loaded = live with { Profile = profile };
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
