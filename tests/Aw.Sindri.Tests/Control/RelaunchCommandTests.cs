using Aw.Sindri.Infrastructure.Control;

namespace Aw.Sindri.Tests.Control;

/// <summary>The restart's relauncher (#3052) starts the installed build through the 'current' link and never the path of the process that is stopping.</summary>
public sealed class RelaunchCommandTests
{
    private const string Old = "/home/u/.agentic-workflow/state/bin/cs/20261101-old/";
    private const string Log = "/home/u/sindri-app/logs/serve.log";

    [Fact, Trait("Lane", "unit")]
    public void The_installed_sindri_is_bin_cs_current_beside_the_running_stamp_folder()
    {
        Assert.Equal("/home/u/.agentic-workflow/state/bin/cs/current/sindri", RelaunchCommand.Installed(Old));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_argv_runs_the_current_link_and_never_the_old_stamp_folder()
    {
        var argv = RelaunchCommand.Argv(Old, 4242, Log)!;

        Assert.Equal(["/bin/sh", "-c"], argv.Take(2));
        Assert.Contains("/home/u/.agentic-workflow/state/bin/cs/current/sindri", argv);
        Assert.DoesNotContain(argv, a => a.Contains("20261101-old", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_argv_names_the_pid_to_wait_for_the_log_and_a_60_second_bound()
    {
        var argv = RelaunchCommand.Argv(Old, 4242, Log)!;

        Assert.Contains("4242", argv);
        Assert.Contains(Log, argv);
        Assert.Contains("kill -0", argv[2], StringComparison.Ordinal);
        Assert.Equal(60, RelaunchCommand.WaitSeconds);
        Assert.Contains("60", argv[2], StringComparison.Ordinal);   // the script, or an argument, carries the bound
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/work/aw/src/Sindri/Aw.Sindri.Host/bin/Debug/net10.0/")]
    [InlineData("/tmp/x/")]
    public void A_process_outside_the_install_layout_has_no_relauncher(string folder)
    {
        Assert.Null(RelaunchCommand.Installed(folder));
        Assert.Null(RelaunchCommand.Argv(folder, 1, Log));
    }
}
