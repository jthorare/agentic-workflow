using System.Diagnostics;
using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Seatbelt;

/// <summary>The process-group start (SB-9c): the argv, and the script run by a real sh as a session leader and as a plain child.</summary>
[Lane(TestLane.Integration)]
public sealed class ProcessGroupScopeArgvTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-pgargv-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Start_is_a_sh_script_the_group_file_and_the_inner_command()
    {
        var argv = ProcessGroupScopeArgv.Start("/l/pgroups", "LCH-abc", ["env", "-i", "x"]);

        Assert.Equal(["/bin/sh", "-c", ProcessGroupScopeArgv.Script, "sh", "/l/pgroups/LCH-abc.pgid", "env", "-i", "x"], argv, StringComparer.Ordinal);
        Assert.Equal(
            "[ \"$(ps -o pgid= -p $$ | tr -d \" \")\" = \"$$\" ] || { echo \"sindri: the session is not its own process group\" >&2; exit 70; }; mkdir -p \"${1%/*}\" && echo $$ > \"$1.tmp\" && mv \"$1.tmp\" \"$1\" && shift && exec \"$@\"",
            ProcessGroupScopeArgv.Script);
    }

    [Fact]
    public void A_unit_with_a_path_separator_is_refused() =>
        _ = Assert.Throws<ArgumentException>(() => ProcessGroupScopeArgv.Start("/l/pgroups", "../x", ["true"]));

    [Fact]
    public async Task A_session_leader_writes_its_group_id_and_runs_the_command()
    {
        Assert.SkipUnless(File.Exists("/usr/bin/setsid") || File.Exists("/bin/setsid"), "setsid is not installed");
        var argv = ProcessGroupScopeArgv.Start(_dir + "/pgroups", "LCH-a", ["/bin/sh", "-c", "exit 0"]);

        var (exit, _) = await Run(["setsid", "-w", .. argv]);

        Assert.Equal(0, exit);
        var written = (await File.ReadAllTextAsync(_dir + "/pgroups/LCH-a.pgid", TestContext.Current.CancellationToken)).Trim();
        Assert.True(int.TryParse(written, NumberStyles.None, CultureInfo.InvariantCulture, out var pgid) && pgid > 1);
        Assert.False(File.Exists(_dir + "/pgroups/LCH-a.pgid.tmp"));
    }

    [Fact]
    public async Task A_session_that_is_not_a_group_leader_is_refused_with_70_and_writes_nothing()
    {
        var argv = ProcessGroupScopeArgv.Start(_dir + "/pgroups", "LCH-b", ["/bin/sh", "-c", "exit 0"]);

        var (exit, error) = await Run(argv);   // a child of the test host shares its group

        Assert.Equal(70, exit);
        Assert.Contains("not its own process group", error, StringComparison.Ordinal);
        Assert.False(File.Exists(_dir + "/pgroups/LCH-b.pgid"));
    }

    private static async Task<(int Exit, string Error)> Run(IReadOnlyList<string> argv)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(argv[0]) { RedirectStandardError = true, UseShellExecute = false } };
        foreach (var a in argv.Skip(1))
        {
            process.StartInfo.ArgumentList.Add(a);
        }

        _ = process.Start();
        var error = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (process.ExitCode, error);
    }
}
