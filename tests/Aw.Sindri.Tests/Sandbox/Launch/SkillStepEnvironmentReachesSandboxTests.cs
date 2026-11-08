using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Sandbox;
using Aw.Sindri.Testing.Skills;
using Aw.Sindri.Tests.Sandbox.Egress;
using Aw.Sindri.Tests.Workflow.Effects;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>
/// A sandboxed session starts from a cleared environment, so a variable the launcher sets for a skill Step reaches it only when the plan carries it (bug 10 of #2148: AW_SINDRI_PR_REF was set and never arrived).
/// The launcher's own set comes from the launcher (an unsandboxed launch of the same Step); nothing here is hand-copied from it.
/// </summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SkillStepEnvironmentReachesSandboxTests : IAsyncLifetime
{
    private static readonly InstanceAddress _instance = InstanceAddress.From("ITM-00000000000000000000000000000001/review/1");

    private readonly string _home = Directory.CreateTempSubdirectory("aw-skill-env-").FullName;
    private readonly TempDirectory _temp = new();
    private TempGitRepo _origin = null!;
    private TempGitRepo _work = null!;

    public async ValueTask InitializeAsync()
    {
        _origin = await TempGitRepo.Create();
        _ = await _origin.Git("update-ref", "refs/pull/2148/head", await _origin.Git("rev-parse", "HEAD"));
        _work = await TempGitRepo.Create();
        _ = await _work.Git("remote", "add", "origin", _origin.Path);
    }

    public ValueTask DisposeAsync()
    {
        _origin.Dispose();
        _work.Dispose();
        _temp.Dispose();
        Directory.Delete(_home, recursive: true);
        return ValueTask.CompletedTask;
    }

    private LaunchRequest SkillStep(string? pr) =>
        A.LaunchRequest().InDirectory(_work.Path).Unattended(new StepArgs(Model: null, Policy: new StepPolicy([], StepPermissionMode.ReadOnly, WritablePath: null), Ref: new StepRef("review", "agent"))).OnInstance(_instance, RunToken.For(1, 1)).Build() with
        {
            RepoPath = _work.Path,
            Skill = new SkillLaunch("review", SkillTarget.ClaudeCode, SkillFixtures.Review, new Dictionary<string, string>(StringComparer.Ordinal) { [PrReference.InputKey] = pr! }.Where(_ => pr is not null).ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal)),
        };

    // The variables the launcher puts in the session's environment: an unsandboxed launch of the Step, so Confine changes none of them.
    private async Task<IReadOnlyDictionary<string, string>> LauncherSet(LaunchRequest request)
    {
        var host = new FakeSessionHost();
        var options = new LauncherOptions(_home) { SindriHome = Path.Combine(_home, "sindri") };
        var launcher = new Launcher(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode]), _ => host, new FakeTerminalViewer(), options, new FakeTimeProvider(KnownInstants.Epoch)) { ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null) });

        var result = await launcher.Launch(request, TestContext.Current.CancellationToken);

        Expect.True(result is StartedLaunch);
        return host.Started[0].Env;
    }

    private async Task<HashSet<string>> PlanNames(LaunchRequest request)
    {
        var stages = new SandboxLaunchStages(new SandboxLaunchStagesTests.NoProfile(), new SandboxLaunchStagesTests.EmptyLearn(), [new FakeModelCredentialSource(ModelLoginSource.OwnerCopy).With(AgentId.From("claude-code"), LoginState.Ready)], new SandboxLaunchStagesTests.StubInputs(_temp.Path), new LaunchSandboxRegistry(new EgressService(new FakeEgressResolver(new Dictionary<string, System.Net.IPAddress[]>(StringComparer.Ordinal)), new FakeEgressDialer(), static _ => "/nonexistent/socat", Path.Combine(_temp.Path, "run"))), new FakeTimeProvider(KnownInstants.Epoch), new AgentTokenRenewal(new FakeAgentTokenSource(), new FakeTimeProvider(KnownInstants.Epoch)));
        var asked = (await stages.Request(request, chosenSession: null, TestContext.Current.CancellationToken)).Value;
        return [.. SandboxPlanner.Build(SandboxMechanism.Bubblewrap, asked).Value.Environment.Select(e => e.Name)];
    }

    // Variables the launcher sets that the plan leaves out on purpose, each with why.
    private static readonly Dictionary<string, string> _onPurpose = new(StringComparer.Ordinal)
    {
        ["AW_SINDRI_LAUNCH_SECRET_FILE"] = "the launch secret goes by file (session wrap reads it); the plan's AW_SINDRI_LAUNCH_SECRET is supplied at start",
        ["GH_TOKEN"] = "Scrub empties it; --clearenv leaves it unset, which is no token either way",
        ["GITHUB_TOKEN"] = "Scrub empties it; --clearenv leaves it unset",
        ["GH_ENTERPRISE_TOKEN"] = "Scrub empties it; --clearenv leaves it unset",
        ["AW_FORGE_TOKEN"] = "Scrub empties it; --clearenv leaves it unset",
        ["AW_REVIEW_GITHUB_TOKEN"] = "Scrub empties it; --clearenv leaves it unset",
    };

    [Fact]
    public async Task Every_variable_the_launcher_sets_for_a_skill_step_reaches_the_sandbox()
    {
        var request = SkillStep(pr: "2148");

        var launcher = await LauncherSet(request);
        var plan = await PlanNames(request);

        Expect.True(launcher.ContainsKey(PrReference.BranchVariable));   // the launcher does set it, so the table below has the PR variable to check
        var missing = launcher.Keys.Where(n => !plan.Contains(n) && !_onPurpose.ContainsKey(n)).Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "set by the launcher, absent from the sandbox plan and not on the on-purpose list: " + string.Join(", ", missing));
        var stale = _onPurpose.Keys.Where(n => !launcher.ContainsKey(n)).ToList();
        Assert.True(stale.Count == 0, "on the on-purpose list but the launcher no longer sets it: " + string.Join(", ", stale));
    }
}
