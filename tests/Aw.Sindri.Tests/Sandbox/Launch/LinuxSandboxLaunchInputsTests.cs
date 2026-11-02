using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Sandbox;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;
using Aw.Sindri.Tests.Sandbox.Egress;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>The Linux sandbox step's box facts: paths, ports, host facts from the machine port, git's common directory, and the short socket directory.</summary>
[Lane(TestLane.Unit)]
public sealed class LinuxSandboxLaunchInputsTests : IDisposable
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-0123456789abcdef0123456789abcdef");

    private readonly TempDirectory _temp = new();
    private readonly FakeProcesses _processes = new();

    public void Dispose() => _temp.Dispose();

    private LinuxSandboxLaunchInputs Make(ControlEndpoint? control = null) =>
        new(new LauncherOptions(_temp.Path) { SindriHome = "/real/home" }, new AgentCatalog([AgentEntries.ClaudeCode]), _processes, () => SandboxRequests.Host, () => control);

    [Fact]
    public void Host_is_what_the_constructor_s_reader_returns() =>
        Expect.True(ReferenceEquals(SandboxRequests.Host, Make().Host()));

    [Fact]
    public void The_sindri_port_is_the_control_plane_s_and_the_proxy_port_is_fixed()
    {
        var inputs = Make(new ControlEndpoint(new Uri("http://127.0.0.1:7480/"), Pid: 1, Epoch: 1));

        Expect.Equal(7480, inputs.SindriPort());
        Expect.Equal(3128, inputs.ProxyPort());
        Expect.Equal(0, Make().SindriPort());
    }

    [Fact]
    public void The_policy_form_comes_from_the_catalog_entry_and_is_none_for_an_unknown_agent()
    {
        var inputs = Make();

        Expect.Equal(AgentEntries.ClaudeCode.PolicyForm, inputs.PolicyFormOf(AgentEntries.ClaudeCode.Id));
        Expect.Equal(PolicyForm.None, inputs.PolicyFormOf(AgentId.From("nobody")));
    }

    [Fact]
    public async Task The_git_common_directory_is_what_git_prints_made_absolute_or_dot_git_when_git_fails()
    {
        _ = _processes.When((_, a) => a.Contains("--git-common-dir", StringComparer.Ordinal), FakeProcesses.Ok("../.git\n"));

        Expect.Equal("/work/.git", await Make().GitCommonDir("/work/web", CancellationToken.None));
        Expect.Equal("/other/.git", await new LinuxSandboxLaunchInputs(new LauncherOptions(_temp.Path), new AgentCatalog([]), new FakeProcesses(), () => SandboxRequests.Host, () => null).GitCommonDir("/other", CancellationToken.None));
    }

    [Fact]
    public void The_net_directory_is_short_enough_for_a_socket_path()
    {
        var dir = LinuxSandboxLaunchInputs.NetDir("/run/user/1000", _launch);

        Expect.Equal("/run/user/1000/aw-net/0123456789ab", dir);
        Expect.True(dir.Length + "/sindri.sock".Length < 100);
    }

    [Fact]
    public void The_run_area_is_per_launch_under_the_launcher_directory() =>
        Expect.Equal(Path.Combine(_temp.Path, "launch", "areas", _launch.Value), Make().RunAreaFor(_launch));

    [Fact]
    public void The_reported_session_is_the_first_line_of_the_launch_s_session_id_file()
    {
        var launches = Path.Combine(_temp.Path, "launches");
        _ = Directory.CreateDirectory(launches);
        File.WriteAllText(Path.Combine(launches, _launch.Value + ".session-id"), "abc\n");

        Expect.Equal("abc", Make().ReportedSession(_launch));
        Expect.True(Make().ReportedSession(LaunchId.From("LCH-ffffffffffffffffffffffffffffffff")) is null);
    }
}
