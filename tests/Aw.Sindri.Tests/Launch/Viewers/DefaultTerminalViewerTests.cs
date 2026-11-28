using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.Viewers;

/// <summary>The 'default-terminal' viewer (#4518): fake 'open', 'xdg-terminal-exec' and 'x-terminal-emulator' scripts on a private PATH, no real terminal.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class DefaultTerminalViewerTests : IDisposable
{
    private readonly ViewerFixture _f = new();

    public void Dispose() => _f.Dispose();

    private string Out(string name) => Path.Combine(_f.Dir, name);

    // Every program runs as "sh <script>" (RunWith), never exec'd: a script written a moment ago can still be open for writing (ETXTBSY), as in ExecTemplateViewerTests.
    private DefaultTerminalViewer Viewer(bool mac, string? grace = null) =>
        new(
            new DefaultTerminalOptions
            {
                IsMac = mac,
                RunWith = "sh",
                Resolve = (program, _) => ValueTask.FromResult(program == "tmux" ? "/opt/t m/tmux" : File.Exists(Out(program)) ? Out(program) : null),
                TempRoot = _f.Dir,
                GetEnv = n => n == "DISPLAY" ? ":99" : n == "AW_META_COMMAND_GRACE" ? grace : null,
            },
            _f.Host,
            _f.Clock);

    private static string Q(string word) => "'" + word.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private string RecordArgs(string name) => _f.Script(name, $"printf '%s\\n' \"$@\" > '{Out(name + ".args")}'; pwd -P > '{Out(name + ".pwd")}'");

    private static async ValueTask<bool> Wait(Task task)
    {
        await task;
        return true;
    }

    private async Task<string> OpenedFile(string openArgs) => (await File.ReadAllLinesAsync(Out(openArgs), TestContext.Current.CancellationToken)).Single();

    [Fact(Timeout = 30_000)]
    public async Task Mac_writes_the_three_line_command_file_in_a_private_folder_and_opens_exactly_that_file()
    {
        await _f.StartSession();
        _ = _f.Script("open", $"printf '%s\\n' \"$@\" > '{Out("open.args")}'");
        var request = _f.Request() with { Cwd = "/tmp/a b/it's" };
        var viewer = Viewer(mac: true);

        var outcome = await _f.Drive(viewer.Open(request, TestContext.Current.CancellationToken));

        Expect.Equal(ViewerOutcome.Unconfirmed, outcome);   // open exits 0 at once and no client attached
        var file = await OpenedFile("open.args");
        Expect.True(file.EndsWith(".command", StringComparison.Ordinal));
        Expect.Equal($"#!/bin/sh\nrm -f -- \"$0\"\ncd {Q("/tmp/a b/it's")} || exit 1\nexec {Q("/opt/t m/tmux")} {string.Join(' ', request.AttachArgv.Skip(1).Select(Q))}\n", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
        Expect.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(file));
        Expect.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Path.GetDirectoryName(file)!));
        _ = await _f.Drive(Wait(viewer.Settled()));
    }

    [Fact(Timeout = 30_000)]
    public async Task Mac_execs_the_agents_own_resolved_program_when_the_host_is_not_tmux()
    {
        await _f.StartSession();
        _ = _f.Script("open", $"printf '%s\\n' \"$@\" > '{Out("open.args")}'");
        _ = _f.Script("claude", "exit 0");
        var request = _f.Request() with { AttachArgv = ["claude", "--x"] };
        var viewer = Viewer(mac: true);

        _ = await _f.Drive(viewer.Open(request, TestContext.Current.CancellationToken));

        var content = await File.ReadAllTextAsync(await OpenedFile("open.args"), TestContext.Current.CancellationToken);
        Expect.True(content.EndsWith($"exec {Q(Out("claude"))} '--x'\n", StringComparison.Ordinal), content);
        Expect.True(!content.Contains("tmux", StringComparison.Ordinal), "a host that is not tmux never gets tmux");
        _ = await _f.Drive(Wait(viewer.Settled()));
    }

    [Fact(Timeout = 30_000)]
    public async Task Mac_removes_the_folder_after_the_grace_even_when_the_file_never_ran()
    {
        await _f.StartSession();
        _ = _f.Script("open", $"printf '%s\\n' \"$@\" > '{Out("open.args")}'");
        var viewer = Viewer(mac: true, grace: "2");
        _ = await _f.Drive(viewer.Open(_f.Request(), TestContext.Current.CancellationToken));
        var file = await OpenedFile("open.args");
        var started = _f.Clock.GetUtcNow();

        Expect.True(File.Exists(file), "the terminal has not run it, so it is still there");
        _ = await _f.Drive(Wait(viewer.Settled()));

        Expect.True(!Directory.Exists(Path.GetDirectoryName(file)), "the folder goes with whatever is left");
        Expect.True(_f.Clock.GetUtcNow() - started <= TimeSpan.FromSeconds(3), "AW_META_COMMAND_GRACE (2 s here) sets the wait, not the 20 s default");
    }

    [Fact(Timeout = 30_000)]
    public async Task Mac_removes_the_folder_as_soon_as_the_file_has_deleted_itself()
    {
        await _f.StartSession();
        _ = _f.Script("open", $"printf '%s\\n' \"$@\" > '{Out("open.args")}'; rm -f -- \"$1\"");   // what the file's first line does when the terminal runs it
        var viewer = Viewer(mac: true);
        _ = await _f.Drive(viewer.Open(_f.Request(), TestContext.Current.CancellationToken));
        var file = await OpenedFile("open.args");

        _ = await _f.Drive(Wait(viewer.Settled()));

        Expect.True(!Directory.Exists(Path.GetDirectoryName(file)));
    }

    [Fact(Timeout = 30_000)]
    public async Task Linux_uses_xdg_terminal_exec_with_the_attach_argv_in_the_request_cwd()
    {
        await _f.StartSession();
        _ = RecordArgs("xdg-terminal-exec");
        _ = RecordArgs("x-terminal-emulator");
        var request = _f.Request();

        _ = await _f.Drive(Viewer(mac: false).Open(request, TestContext.Current.CancellationToken));

        Expect.Sequence([.. request.AttachArgv], await File.ReadAllLinesAsync(Out("xdg-terminal-exec.args"), TestContext.Current.CancellationToken));
        Expect.Equal(Path.GetFullPath(_f.Dir), (await File.ReadAllTextAsync(Out("xdg-terminal-exec.pwd"), TestContext.Current.CancellationToken)).Trim());
        Expect.True(!File.Exists(Out("x-terminal-emulator.args")), "the fallback is not run when xdg-terminal-exec exists");
    }

    [Fact(Timeout = 30_000)]
    public async Task Linux_falls_back_to_x_terminal_emulator_dash_e()
    {
        await _f.StartSession();
        _ = RecordArgs("x-terminal-emulator");
        var request = _f.Request();

        _ = await _f.Drive(Viewer(mac: false).Open(request, TestContext.Current.CancellationToken));

        Expect.Sequence(["-e", .. request.AttachArgv], await File.ReadAllLinesAsync(Out("x-terminal-emulator.args"), TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 30_000)]
    public async Task Linux_with_neither_terminal_fails_naming_both_and_spawns_nothing()
    {
        await _f.StartSession();

        var outcome = await _f.Drive(Viewer(mac: false).Open(_f.Request(), TestContext.Current.CancellationToken));

        Expect.Equal(ViewerOutcomeKind.Failed, outcome.Kind);
        Expect.Equal(SindriErrors.LaunchViewerFailed.Code, outcome.Code);
        Expect.True(outcome.Detail!.Contains("xdg-terminal-exec", StringComparison.Ordinal) && outcome.Detail.Contains("x-terminal-emulator", StringComparison.Ordinal));
        Expect.True(!File.Exists(Out("xdg-terminal-exec.args")) && !File.Exists(Out("x-terminal-emulator.args")));
        Expect.True(Directory.EnumerateFileSystemEntries(_f.Dir).All(static e => !e.Contains("aw-term-", StringComparison.Ordinal)), "nothing was written or spawned");
    }

    [Fact(Timeout = 30_000)]
    public async Task Probe_names_what_is_missing()
    {
        var missingMac = await Viewer(mac: true).Probe(TestContext.Current.CancellationToken);
        var missingLinux = await Viewer(mac: false).Probe(TestContext.Current.CancellationToken);
        _ = _f.Script("x-terminal-emulator", "exit 0");
        _ = _f.Script("open", "exit 0");
        var linux = await Viewer(mac: false).Probe(TestContext.Current.CancellationToken);
        var mac = await Viewer(mac: true).Probe(TestContext.Current.CancellationToken);

        Expect.Equal("default-terminal", missingMac.Kind);
        Expect.Equal(("not-installed", "not-installed"), (missingMac.Problem, missingLinux.Problem));
        Expect.True(linux.Present && mac.Present);
    }
}
