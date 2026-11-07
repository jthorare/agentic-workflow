using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>#2163: a plan that leaves AW_SINDRI_LAUNCH_SECRET to the launcher is refused when no value is registered for the launch, not wrapped plain (the agent would start with no secret).</summary>
[Lane(TestLane.Unit)]
public sealed class WrapWithoutLaunchSecretTests
{
    private const string Variable = "AW_SINDRI_LAUNCH_SECRET";

    [Fact]
    public void Bubblewrap_refuses_a_plan_with_no_registered_launch_secret_and_names_the_variable()
    {
        using var area = new TempRunArea(Variable);
        var options = new BubblewrapOptions("bwrap", "systemd-run", "socat", "git", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20));
        var sandbox = new BubblewrapSandbox(options, new FakeProcesses(), new HostPaths(), new LaunchSecretRegistry(), new FakeTimeProvider());

        var wrapped = sandbox.Wrap(area.Plan, ["claude"]);

        Assert.True(wrapped.IsError);
        Assert.Equal("SND-LAUNCH-016", wrapped.Error.Code);
        Assert.Contains(Variable, wrapped.Error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(area.Root + "/area/secrets"));
    }

    [Fact]
    public void Seatbelt_refuses_a_plan_with_no_registered_launch_secret_and_names_the_variable()
    {
        using var area = new TempRunArea(Variable);
        var options = new SeatbeltOptions("sandbox-exec", "git", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20));
        var sandbox = new SeatbeltSandbox(options, new FakeProcesses(), new LaunchSecretRegistry(), new FakeTimeProvider(), _ => 51234, _ => area.Root + "/short");

        var wrapped = sandbox.Wrap(area.Plan, ["claude"]);

        Assert.True(wrapped.IsError);
        Assert.Equal("SND-LAUNCH-016", wrapped.Error.Code);
        Assert.Contains(Variable, wrapped.Error.Message, StringComparison.Ordinal);
    }
}
