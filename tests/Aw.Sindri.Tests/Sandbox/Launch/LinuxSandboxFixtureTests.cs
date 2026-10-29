using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>The fixture plans Check again self-checks (sandbox spec §8): the ReadOnly one, and a WriteScope one so the git canary runs in 'sindri sandbox check' too.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LinuxSandboxFixtureTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-fixture-").FullName;

    // why: the fixture lists the directories beside HOME as "other homes"; with HOME straight under /tmp that is every leftover directory on the box, so HOME sits alone in a folder of its own.
    private readonly string _home;

    public LinuxSandboxFixtureTests()
    {
        _home = Directory.CreateDirectory(Path.Combine(_dir, "homes", "owner")).FullName;
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private LinuxSandboxFixture Fixture(FakeProcesses processes) =>
        new(_dir + "/sindri", name => name switch { "HOME" => _home, "AW_STATE_DIR" => _dir + "/state", "XDG_RUNTIME_DIR" => _dir + "/run", _ => null }, processes, new Machine());

    private static string[] Canaries(SandboxRequest request)
    {
        var plan = SandboxPlanner.Build(SandboxMechanism.PassThrough, request);
        Assert.True(plan.IsSuccess, plan.IsError ? plan.Error.Message : string.Empty);
        return [.. SandboxCanaries.For(plan.Value).Select(c => c.Reason)];
    }

    [Fact]
    public void The_write_scope_fixture_plan_includes_the_git_ref_canary()
    {
        var request = Fixture(new FakeProcesses().When(static (_, _) => true, FakeProcesses.Ok())).WriteScopeRequest();

        Assert.NotNull(request);
        Assert.Contains("git-ref", Canaries(request), StringComparer.Ordinal);
    }

    [Fact]
    public void The_fixture_requests_leave_the_folders_and_session_home_the_plan_binds_in_place()
    {
        var fixture = Fixture(new FakeProcesses().When(static (_, _) => true, FakeProcesses.Ok()));

        foreach (var request in new[] { fixture.Request(), fixture.WriteScopeRequest() })
        {
            Assert.NotNull(request);
            var session = Aw.Sindri.Core.Sandbox.Plan.SessionHomePaths.For(_dir + "/sindri", request.Launch);
            Assert.True(File.Exists(session + "/control.json"));
            Assert.True(Directory.Exists(session + "/gh-config"));
            Assert.True(Directory.Exists(_dir + "/sindri/launches"));
            Assert.True(Directory.Exists(_dir + "/sindri/approved"));
        }
    }

    [Fact]
    public void The_read_only_fixture_plan_has_no_git_ref_canary()
    {
        var request = Fixture(new FakeProcesses().When(static (_, _) => true, FakeProcesses.Ok())).Request();

        Assert.NotNull(request);
        Assert.DoesNotContain("git-ref", Canaries(request), StringComparer.Ordinal);
    }

    [Fact]
    public void The_write_scope_fixture_makes_a_real_worktree_on_the_run_branch()
    {
        var processes = new FakeProcesses().When(static (_, _) => true, FakeProcesses.Ok());

        var request = Fixture(processes).WriteScopeRequest();

        Assert.NotNull(request);
        Assert.Equal(request.Paths.Worktree, request.Policy?.WritablePath);
        Assert.Contains(processes.Calls, c => c.Arguments.Contains("worktree", StringComparer.Ordinal) && c.Arguments.Contains("sindri/fixture/1/work", StringComparer.Ordinal));
    }

    [Fact]
    public void A_box_where_git_fails_has_no_write_scope_fixture_and_the_self_check_is_skipped() =>
        Assert.Null(Fixture(new FakeProcesses()).WriteScopeRequest());

    private sealed class Machine : ISandboxMachine
    {
        public long RamBytes => 16L * 1024 * 1024 * 1024;

        public int Cores => 8;
    }
}
