using Aw.Kernel.Testing;
using Aw.Sindri.Host.Commands.SessionWrap;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>The real child process of 'session wrap': a short-lived /bin/sh on a bounded wait, disposed (and killed by its own handle) at the end of each test.</summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ProcessChildLauncherTests
{
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(20);

    private static Dictionary<string, string> Env(params (string Key, string Value)[] pairs) => pairs.ToDictionary(static p => p.Key, static p => p.Value, StringComparer.Ordinal);

    [Fact]
    public async Task The_childs_exit_code_comes_back()
    {
        var started = new ProcessChildLauncher().Start(["/bin/sh", "-c", "exit 7"], Env());
        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        using var child = started.Value;

        var code = await child.Exited.WaitAsync(_deadline, TimeProvider.System, TestContext.Current.CancellationToken);

        Expect.Equal(7, code);
    }

    [Fact]
    public async Task The_child_sees_the_variables_wrap_sets_on_top_of_the_inherited_ones()
    {
        var started = new ProcessChildLauncher().Start(["/bin/sh", "-c", "test \"$AW_SINDRI_CHILD\" = 1 && test -n \"$PATH\" && exit 3; exit 4"], Env(("AW_SINDRI_CHILD", "1")));
        using var child = started.Value;

        var code = await child.Exited.WaitAsync(_deadline, TimeProvider.System, TestContext.Current.CancellationToken);

        Expect.Equal(3, code);
    }

    [Fact]
    public void A_command_that_cannot_start_is_an_error_not_a_throw()
    {
        var started = new ProcessChildLauncher().Start(["/no/such/agent-cli"], Env());

        Expect.True(started.IsError);
    }

    [Fact]
    public async Task Disposing_a_running_child_kills_it()
    {
        var started = new ProcessChildLauncher().Start(["/bin/sh", "-c", "sleep 60"], Env());
        var child = started.Value;

        child.Dispose();

        var code = await child.Exited.WaitAsync(_deadline, TimeProvider.System, TestContext.Current.CancellationToken);
        Expect.True(code != 0);
    }
}
