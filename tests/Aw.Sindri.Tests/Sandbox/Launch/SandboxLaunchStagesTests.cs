using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Sandbox;
using Aw.Sindri.Testing.Skills;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;
using Aw.Sindri.Tests.Sandbox.Egress;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>The sandbox step's launch parts: the plan request from the launch, the login and the learn history; the egress and its release.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxLaunchStagesTests : IDisposable
{
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeAgentTokenSource _tokens = new(Results.Success(new AgentToken("ghs_first", KnownInstants.Epoch.AddHours(1))));
    private readonly StubInputs _inputs;
    private readonly TempDirectory _temp = new();
    private readonly FakeModelCredentialSource _login = new FakeModelCredentialSource(ModelLoginSource.OwnerCopy).With(AgentId.From("claude-code"), LoginState.Ready);

    private SandboxLaunchStages Make(params IModelCredentialSource[] logins) =>
        new(new NoProfile(), new EmptyLearn(), logins.Length == 0 ? [_login] : logins, _inputs, Registry(), _clock, new AgentTokenRenewal(_tokens, _clock));

    private SandboxLaunchStages MakeWith(GithubCredentials github) =>
        new(new GithubProfile(github), new EmptyLearn(), [_login], _inputs, Registry(), _clock, new AgentTokenRenewal(_tokens, _clock));

    public SandboxLaunchStagesTests()
    {
        _inputs = new StubInputs(_temp.Path);
    }

    private string Home => Path.Combine(_temp.Path, "home");

    public void Dispose() => _temp.Dispose();

    private LaunchSandboxRegistry Registry() =>
        new(new EgressService(new FakeEgressResolver(new Dictionary<string, System.Net.IPAddress[]>(StringComparer.Ordinal)), new FakeEgressDialer(), static _ => "/nonexistent/socat", Path.Combine(_temp.Path, "run")));

    // why: the planner refuses a Step with no policy, so the tests that build a plan use one
    private static LaunchRequest PlannedStep() =>
        A.LaunchRequest().InDirectory("/work/web").Unattended(new StepArgs(Model: null, Policy: new StepPolicy([], StepPermissionMode.ReadOnly, WritablePath: null), Ref: new StepRef("build", "agent"))).OnInstance(InstanceAddress.From("ITM-1/spec/1"), RunToken.For(1, 1)).Build();

    private static LaunchRequest Step() =>
        A.LaunchRequest().InDirectory("/work/web").Unattended(new StepArgs(Model: null, Policy: null, Ref: new StepRef("build", "agent"))).OnInstance(InstanceAddress.From("ITM-1/spec/1"), RunToken.For(1, 1)).Build();   // a real Step: an instance address with slashes and a run token

    [Fact]
    public async Task Request_names_the_launch_paths_net_host_login_and_learn_history()
    {
        var request = Step();

        var built = await Make().Request(request, chosenSession: null, CancellationToken.None);

        Expect.True(built.IsSuccess);
        var r = built.Value;
        Expect.Equal(request.Launch, r.Launch);
        Expect.Equal(SandboxRequests.SindriHome + "/launch/areas/" + request.Launch.Value, r.Paths.RunArea);
        Expect.Equal(SandboxRequests.SindriHome + "/workflow/runs/WFI-1/build/" + request.Launch.Value, r.Paths.RunDirectory);
        Expect.Equal("/net/" + request.Launch.Value, r.Net.NetDir);
        Expect.Equal(7480, r.Net.SindriPort);
        Expect.True(!r.Attended && r.Login is { Source: ModelLoginSource.OwnerCopy });
        Expect.Equal("sindri/" + WorkflowInstanceId.Of(InstanceAddress.From("ITM-1/spec/1")).Value + "/1", r.RunNamespace);   // the instance id, never the address with its slashes
        Expect.True(r.Learn is { } learn && learn.Now == _clock.GetUtcNow());
        Expect.Equal("web", r.Repo.Value);   // no profile repo: the directory's name
    }

    [Fact]
    public async Task Request_passes_the_Steps_timeout_and_a_plain_Step_keeps_the_longMax_cap()
    {
        var request = Step() with { StepTimeout = TimeSpan.FromHours(48) };

        var built = await MakeWith(GithubCredentials.None).Request(request, chosenSession: null, CancellationToken.None);

        Expect.True(built.IsSuccess);
        Expect.Equal(TimeSpan.FromHours(48), built.Value.StepTimeout);
        Expect.Equal(TimeSpan.FromHours(2), built.Value.LongMax);   // workflow.steps.longMax, default 2h
    }

    [Fact]
    public async Task A_skill_launch_skips_the_longMax_cap_so_a_48h_state_timeout_plans_a_48h_wall_plus_grace()
    {
        var request = Step() with { StepTimeout = TimeSpan.FromHours(48), Skill = new SkillLaunch("review", SkillTarget.ClaudeCode, SkillFixtures.Review, new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = "42" }) };

        var built = await MakeWith(GithubCredentials.None).Request(request, chosenSession: null, CancellationToken.None);

        Expect.True(built.IsSuccess);
        Expect.Equal(TimeSpan.FromHours(48), built.Value.StepTimeout);
        Expect.True(built.Value.LongMax is null);   // the state timeout is the only bound (RCP-SKILL-001)
        var limits = LimitsResolver.Resolve(new LimitsInput(Limits: null, AttendedSettings: null, Attended: false, built.Value.StepTimeout, built.Value.LongMax, RamBytes: 32L * 1024 * 1024 * 1024, Cores: 8));
        Expect.Equal(TimeSpan.FromHours(48) + limits.WallGrace, limits.Wall);
    }

    private static LaunchRequest SkillStep(Dictionary<string, string> inputs) =>
        PlannedStep() with { Skill = new SkillLaunch("review", SkillTarget.ClaudeCode, SkillFixtures.Review, inputs) };

    private async Task<SandboxPlan> PlanOf(LaunchRequest request)
    {
        var asked = (await Make().Request(request, chosenSession: null, CancellationToken.None)).Value;
        return SandboxPlanner.Build(SandboxMechanism.Bubblewrap, asked).Value;
    }

    [Fact]
    public async Task A_skill_launch_with_a_pr_input_sets_the_branch_the_host_fetched_the_PR_head_to_inside_the_sandbox()
    {
        var request = SkillStep(new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = "2148" });
        var branch = PrReference.BranchOf(WorkflowInstanceId.Of(request.Instance!.Value), request.Run!.Value.Number);

        var plan = await PlanOf(request);
        var argv = BubblewrapArgv.Build(plan, new FakeHostPaths(), secretsFd: false).ToList();

        Expect.Equal(branch, plan.Environment.Single(e => string.Equals(e.Name, PrReference.BranchVariable, StringComparison.Ordinal)).Value);
        Expect.True(argv.Zip(argv.Skip(1), argv.Skip(2)).Any(t => t is ("--setenv", var name, var value) && string.Equals(name, PrReference.BranchVariable, StringComparison.Ordinal) && string.Equals(value, branch, StringComparison.Ordinal)));   // the real bubblewrap argv, after --clearenv
    }

    [Fact]
    public async Task A_skill_launch_without_a_pr_input_gets_no_PR_variable_and_the_same_environment_names_as_before()
    {
        var plan = await PlanOf(SkillStep(new Dictionary<string, string>(StringComparer.Ordinal)));
        var plain = await PlanOf(PlannedStep());

        Expect.True(plan.Environment.All(e => !string.Equals(e.Name, PrReference.BranchVariable, StringComparison.Ordinal)));
        string[] skillVariables = ["AW_RESULT", "AW_RESULT_SKILL", "AW_RESULT_FILE", "AW_DIR"];
        Expect.True(plan.Environment.Select(e => e.Name).Order(StringComparer.Ordinal).SequenceEqual(plain.Environment.Select(e => e.Name).Concat(skillVariables).Order(StringComparer.Ordinal), StringComparer.Ordinal));
    }

    [Fact]
    public async Task Request_refuses_an_agent_app_Step_with_the_token_code_when_the_mint_fails()
    {
        _tokens.Then(SandboxRefusal.For(SindriErrors.LaunchGitHubTokenFailed, SandboxReasons.NoKey));

        var built = await MakeWith(GithubCredentials.AgentApp).Request(Step(), chosenSession: null, CancellationToken.None);

        Expect.True(built.IsError && string.Equals(built.Error.Code, SindriErrors.LaunchGitHubTokenFailed.Code, StringComparison.Ordinal));
        Expect.Equal(SandboxReasons.NoKey, built.Error.Extensions!.Single(e => e.Key == "reason").Value.GetString());
    }

    [Fact]
    public async Task WritePolicy_puts_the_agent_app_token_in_a_0600_file_in_a_0700_folder_of_the_session_home()
    {
        var stages = MakeWith(GithubCredentials.AgentApp);
        var asked = (await stages.Request(PlannedStep(), chosenSession: null, CancellationToken.None)).Value;
        var plan = SandboxPlanner.Build(SandboxMechanism.Bubblewrap, asked).Value;

        var written = await stages.WritePolicy(asked, plan, Home, CancellationToken.None);

        Expect.True(written.IsSuccess);
        Expect.Equal("ghs_first\n", await File.ReadAllTextAsync(SessionHomePaths.GhTokenFile(Home), Token));
        Expect.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, Mode(SessionHomePaths.GhTokenFile(Home)));
        Expect.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, Mode(SessionHomePaths.GhTokenDirectory(Home)));
        Expect.Equal(1, Directory.GetFileSystemEntries(SessionHomePaths.GhTokenDirectory(Home)).Length);   // no temp file left behind
        await stages.Release(plan.Launch, CancellationToken.None);
    }

    [Fact]
    public async Task The_token_is_renewed_every_45_minutes_by_renaming_a_new_file_in()
    {
        var (stages, plan) = await WriteAgentAppToken();
        _tokens.Then(Results.Success(new AgentToken("ghs_second", KnownInstants.Epoch.AddHours(2))));
        await using var old = File.OpenRead(SessionHomePaths.GhTokenFile(Home));   // the first file's inode, kept open

        _clock.Advance(TimeSpan.FromMinutes(44));
        Expect.Equal(1, _tokens.Calls);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await WaitUntil(() => File.ReadAllText(SessionHomePaths.GhTokenFile(Home)) == "ghs_second\n");

        using var oldText = new StreamReader(old);
        Expect.Equal("ghs_first\n", await oldText.ReadToEndAsync(Token));   // replaced by rename, never rewritten in place
        Expect.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, Mode(SessionHomePaths.GhTokenFile(Home)));
        await stages.Release(plan.Launch, CancellationToken.None);
    }

    [Fact]
    public async Task A_renewal_that_throws_is_said_and_retried_not_dropped()
    {
        var warnings = new List<string>();
        var (stages, plan) = await WriteAgentAppToken(warnings.Add);
        _tokens.ThrowNext = new InvalidOperationException("the keychain process did not start");
        _tokens.Then(Results.Success(new AgentToken("ghs_after_throw", KnownInstants.Epoch.AddHours(2))));

        _clock.Advance(TimeSpan.FromMinutes(45));
        await WaitUntil(() => warnings.Count == 1);
        Expect.True(warnings[0].Contains("SND-LAUNCH-018", StringComparison.Ordinal) && warnings[0].Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
        await WaitUntil(() =>
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            return File.ReadAllText(SessionHomePaths.GhTokenFile(Home)) == "ghs_after_throw\n";
        });
        await stages.Release(plan.Launch, CancellationToken.None);
    }

    [Fact]
    public async Task A_renewal_after_the_session_home_is_gone_writes_nothing_back()
    {
        var (stages, plan) = await WriteAgentAppToken();
        _tokens.Then(Results.Success(new AgentToken("ghs_late", KnownInstants.Epoch.AddHours(2))));
        Directory.Delete(SessionHomePaths.GhTokenDirectory(Home), recursive: true);

        _clock.Advance(TimeSpan.FromMinutes(45));
        await WaitUntil(() => _tokens.Calls == 2);
        _clock.Advance(TimeSpan.FromMinutes(45));
        Expect.True(!Directory.Exists(SessionHomePaths.GhTokenDirectory(Home)));
        Expect.Equal(2, _tokens.Calls);   // a closed launch is not renewed again
        await stages.Release(plan.Launch, CancellationToken.None);
    }

    [Fact]
    public void A_renewal_write_finds_the_token_folder_gone_and_makes_none()
    {
        var gone = Path.Combine(_temp.Path, "removed-session-home");

        Expect.True(!TokenFile.Replace(gone, "ghs_late", create: false));
        Expect.True(!Directory.Exists(gone));
    }

    [Fact]
    public async Task A_failed_renewal_keeps_the_old_token_warns_and_tries_again_in_5_minutes()
    {
        var warnings = new List<string>();
        var (stages, plan) = await WriteAgentAppToken(warnings.Add);
        _tokens.Then(SandboxRefusal.For(SindriErrors.LaunchGitHubTokenFailed, "http-401"), Results.Success(new AgentToken("ghs_retry", KnownInstants.Epoch.AddHours(2))));

        _clock.Advance(TimeSpan.FromMinutes(45));
        await WaitUntil(() => _tokens.Calls == 2);   // the first call is the Request's own mint
        await WaitUntil(() => warnings.Count == 1);
        Expect.Equal("ghs_first\n", await File.ReadAllTextAsync(SessionHomePaths.GhTokenFile(Home), Token));
        Expect.True(warnings[0].Contains("SND-LAUNCH-018", StringComparison.Ordinal) && warnings[0].Contains("http-401", StringComparison.Ordinal));

        var minutes = 0;   // the retry timer is set just after the warning, so step a minute at a time and count
        await WaitUntil(() =>
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            minutes++;
            return File.ReadAllText(SessionHomePaths.GhTokenFile(Home)) == "ghs_retry\n";
        });
        Expect.True(minutes <= 6);
        await stages.Release(plan.Launch, CancellationToken.None);
    }

    [Fact]
    public async Task Release_stops_the_renewals()
    {
        var (stages, plan) = await WriteAgentAppToken();

        await stages.Release(plan.Launch, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(3));
        await Task.Delay(100, Token);

        Expect.Equal(1, _tokens.Calls);
    }

    private static LaunchRequest AgentAppSkillStep(SkillBlockManifest block, bool trial) =>
        PlannedStep() with { Skill = new SkillLaunch("skill", SkillTarget.ClaudeCode, block, new Dictionary<string, string>(StringComparer.Ordinal)) { Trial = trial } };

    [Theory]
    [InlineData(false, false)]   // a block that does not push
    [InlineData(true, true)]     // a pushing block run as a trial
    public async Task Request_mints_no_agent_token_for_a_skill_Step_that_cannot_push(bool pushingBlock, bool trial)
    {
        var block = pushingBlock ? SkillFixtures.BugFixOrchestrator : SkillFixtures.Review;

        var built = await MakeWith(GithubCredentials.AgentApp).Request(AgentAppSkillStep(block, trial), chosenSession: null, CancellationToken.None);
        var plan = SandboxPlanner.Build(SandboxMechanism.Bubblewrap, built.Value).Value;

        Expect.Equal(0, _tokens.Calls);
        Expect.Equal(GithubCredentials.None, plan.Github);
        Expect.True(!plan.Network.Groups.Any(g => string.Equals(g.Name, "github", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Request_mints_the_agent_token_for_a_skill_Step_whose_block_pushes()
    {
        var stages = MakeWith(GithubCredentials.AgentApp);

        var built = await stages.Request(AgentAppSkillStep(SkillFixtures.BugFixOrchestrator, trial: false), chosenSession: null, CancellationToken.None);
        var plan = SandboxPlanner.Build(SandboxMechanism.Bubblewrap, built.Value).Value;

        Expect.Equal(1, _tokens.Calls);
        Expect.Equal(GithubCredentials.AgentApp, plan.Github);
        await stages.Release(plan.Launch, CancellationToken.None);
    }

    // A pushing skill Step carrying its ask (StepPolicy.Github) on a profile that gives no GitHub credential.
    private static LaunchRequest AskingSkillStep(bool approved)
    {
        var step = AgentAppSkillStep(SkillFixtures.BugFixOrchestrator, trial: false);
        return step with { Step = step.Step! with { Policy = new StepPolicy([], StepPermissionMode.ReadOnly, WritablePath: null, Github: GithubCredentials.AgentApp) } } with { GithubPushApproved = approved };
    }

    [Theory]
    [InlineData(true, 1, GithubCredentials.AgentApp)]
    [InlineData(false, 0, GithubCredentials.None)]   // an ask with no approval stays scrubbed
    public async Task Request_mints_and_plans_the_same_mode_for_a_Steps_ask(bool approved, int mints, GithubCredentials expected)
    {
        var stages = MakeWith(GithubCredentials.None);

        var built = await stages.Request(AskingSkillStep(approved), chosenSession: null, CancellationToken.None);
        var plan = SandboxPlanner.Build(SandboxMechanism.Bubblewrap, built.Value).Value;

        Expect.Equal(mints, _tokens.Calls);
        Expect.Equal(expected, plan.Github);
        await stages.Release(plan.Launch, CancellationToken.None);
    }

    [Fact]
    public async Task A_trial_run_never_gets_the_token_even_with_an_approved_ask()
    {
        var step = AskingSkillStep(approved: true);
        step = step with { Skill = step.Skill! with { Trial = true } };

        var built = await MakeWith(GithubCredentials.None).Request(step, chosenSession: null, CancellationToken.None);
        var plan = SandboxPlanner.Build(SandboxMechanism.Bubblewrap, built.Value).Value;

        Expect.Equal(0, _tokens.Calls);
        Expect.Equal(GithubCredentials.None, plan.Github);
    }

    private async Task<(SandboxLaunchStages Stages, SandboxPlan Plan)> WriteAgentAppToken(Action<string>? warn = null)
    {
        var stages = new SandboxLaunchStages(new GithubProfile(GithubCredentials.AgentApp), new EmptyLearn(), [_login], _inputs, Registry(), _clock, new AgentTokenRenewal(_tokens, _clock, warn));
        var asked = (await stages.Request(PlannedStep(), chosenSession: null, CancellationToken.None)).Value;
        var plan = SandboxPlanner.Build(SandboxMechanism.Bubblewrap, asked).Value;
        Expect.True((await stages.WritePolicy(asked, plan, Home, CancellationToken.None)).IsSuccess);
        return (stages, plan);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static UnixFileMode Mode(string path) => OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(path);

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(10, Token);
        }

        Expect.True(condition());
    }

    [Fact]
    public async Task Request_leaves_a_Step_with_no_GitHub_credential_alone()
    {
        var built = await MakeWith(GithubCredentials.None).Request(Step(), chosenSession: null, CancellationToken.None);

        Expect.True(built.IsSuccess);
    }

    [Fact]
    public async Task Request_refuses_with_the_login_code_when_no_source_supports_the_agent()
    {
        var built = await Make(new FakeModelCredentialSource(ModelLoginSource.OwnerCopy)).Request(Step(), chosenSession: null, CancellationToken.None);

        Expect.True(built.IsError && string.Equals(built.Error.Code, SindriErrors.LaunchLoginUnavailable.Code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartEgress_without_socat_refuses_with_the_unavailable_code_and_Release_is_a_no_op()
    {
        var stages = Make();
        var (_, plan) = await PlanFor(new StepPolicy([], StepPermissionMode.Default, WritablePath: null));

        var started = await stages.StartEgress(plan, CancellationToken.None);
        await stages.Release(plan.Launch, CancellationToken.None);

        Expect.True(started.IsError && string.Equals(started.Error.Code, SindriErrors.LaunchSandboxUnavailable.Code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_claude_Step_gets_its_policy_file_rewritten_with_the_plan_and_no_extra_arguments()
    {
        var policy = new StepPolicy([], StepPermissionMode.Default, WritablePath: null);
        var (asked, plan) = await PlanFor(policy);
        _inputs.Form = PolicyForm.ClaudeSettingsFile;

        var args = await Make().WritePolicy(asked, plan, Home, CancellationToken.None);

        Expect.True(args.IsSuccess && args.Value.Args.Count == 0);
        Expect.True(File.Exists(StepPolicyFile.PathFor(Home, plan.Launch)) && args.Value.To == StepPolicyFile.PathFor(Home, plan.Launch));
        Expect.True(!File.Exists(StepPolicyFile.PathFor(_inputs.PolicyDirectory(), plan.Launch)));
        Expect.True(args.Value.Apply(["claude", "--settings", args.Value.From!])[2] == args.Value.To);   // the argv points at the file in the session home
    }

    [Fact]
    public async Task A_codex_Step_gets_the_override_arguments_and_no_file()
    {
        var policy = new StepPolicy([], StepPermissionMode.Default, WritablePath: null);
        var (asked, plan) = await PlanFor(policy);
        _inputs.Form = PolicyForm.CodexOverrides;

        var args = await Make().WritePolicy(asked, plan, Home, CancellationToken.None);

        Expect.True(args.IsSuccess && args.Value.Args.Count > 0);
        Expect.True(!File.Exists(StepPolicyFile.PathFor(Home, plan.Launch)));
    }

    [Fact]
    public void Extra_arguments_go_right_after_the_program_and_before_the_prompt()
    {
        var files = new PolicyFiles(["-c", "approval_policy=\"never\""]);

        var argv = files.Apply(["codex", "exec", "--", "do the thing"]);

        Expect.True(argv.SequenceEqual(["codex", "-c", "approval_policy=\"never\"", "exec", "--", "do the thing"], StringComparer.Ordinal));
    }

    private static LaunchRequest Asked(StepPolicy policy) =>
        A.LaunchRequest().InDirectory("/work/web").Unattended(new StepArgs(Model: null, Policy: policy, Ref: new StepRef("build", "agent"))).OnInstance(InstanceAddress.From("ITM-1/spec/1"), RunToken.For(1, 1)).Build();

    private async Task<(SandboxRequest Request, SandboxPlan Plan)> PlanFor(StepPolicy policy)
    {
        var asked = (await Make().Request(Asked(policy), chosenSession: null, CancellationToken.None)).Value;
        return (asked, SandboxPlanner.Build(SandboxMechanism.Bubblewrap, asked).Value);
    }

    internal sealed class NoProfile : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<LoadedProfile?>(value: null));
    }

    private sealed class GithubProfile(GithubCredentials github) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success<LoadedProfile?>(new LoadedProfile(
                new SindriProfile(1, "thor", Sandbox: new SandboxSettings(Credentials: new SandboxCredentials(github))), ProfileHashing.Of("{}"u8), "/home/profile.yaml")));
    }

    internal sealed class EmptyLearn : ILearnHistory
    {
        public ValueTask<LearnHistory> Read(string repo, CancellationToken cancellationToken) => ValueTask.FromResult(new LearnHistory(FirstLearnRun: null, LearnRuns: 0, EndedAt: null));
    }

    internal sealed class StubInputs(string directory) : ISandboxLaunchInputs
    {
        public PolicyForm Form { get; set; }

        public string PolicyDirectory() => directory;

        public PolicyForm PolicyFormOf(AgentId agent) => Form;

        public DenialCounter? DenialCounterFor(LaunchId launch) => null;

        public SandboxHostFacts Host() => SandboxRequests.Host;

        public ValueTask<string> GitCommonDir(string repoPath, CancellationToken cancellationToken) => ValueTask.FromResult(repoPath + "/.git");

        public string RunAreaFor(LaunchId launch) => SandboxRequests.SindriHome + "/launch/areas/" + launch.Value;

        public string? RunDirectoryFor(LaunchRequest request) => SandboxRequests.SindriHome + "/workflow/runs/WFI-1/build/" + request.Launch.Value;

        public string NetDirFor(LaunchId launch) => "/net/" + launch.Value;

        public int SindriPort() => 7480;

        public int ProxyPort() => 3128;

        public string? TranscriptGlob(AgentId agent) => null;

        public string? ReportedSession(LaunchId launch) => null;
    }
}
