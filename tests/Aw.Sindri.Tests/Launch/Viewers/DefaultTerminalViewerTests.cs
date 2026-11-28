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

    private DefaultTerminalViewer Viewer(bool mac, params string[] onPath) =>
        new(
            new DefaultTerminalOptions
            {
                IsMac = mac,
                OnPath = (program, _) => ValueTask.FromResult(onPath.Contains(program, StringComparer.Ordinal)),
                Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["PATH"] = _f.Dir + ":/usr/bin:/bin" },
                TempRoot = _f.Dir,
                GetEnv = static n => n == "DISPLAY" ? ":99" : null,
            },
            _f.Host,
            _f.Clock);

    private static string Q(string word) => "'" + word.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private string RecordArgs(string name) => _f.Script(name, $"printf '%s\\n' \"$@\" > '{Out(name + ".args")}'; pwd -P > '{Out(name + ".pwd")}'");

    [Fact(Timeout = 30_000)]
    public async Task Mac_writes_a_private_command_file_quotes_every_word_and_opens_exactly_that_file()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the fake open reads modes with GNU stat");
        await _f.StartSession();
        _ = _f.Script("open", $"cp \"$1\" '{Out("copy")}'; stat -c %a \"$1\" > '{Out("filemode")}'; stat -c %a \"$(dirname \"$1\")\" > '{Out("dirmode")}'; printf '%s\\n' \"$@\" > '{Out("open.args")}'");
        var request = _f.Request() with { Cwd = "/tmp/a b/it's" };
        var viewer = Viewer(mac: true, "open");

        var outcome = await _f.Drive(viewer.Open(request, TestContext.Current.CancellationToken));

        Expect.Equal(ViewerOutcome.Unconfirmed, outcome);   // open exits 0 at once and no client attached
        var content = await File.ReadAllTextAsync(Out("copy"), TestContext.Current.CancellationToken);
        Expect.Equal($"#!/bin/sh\ncd {Q("/tmp/a b/it's")}; exec {string.Join(' ', request.AttachArgv.Select(Q))}\n", content);
        Expect.Equal("700", (await File.ReadAllTextAsync(Out("filemode"), TestContext.Current.CancellationToken)).Trim());
        Expect.Equal("700", (await File.ReadAllTextAsync(Out("dirmode"), TestContext.Current.CancellationToken)).Trim());
        var opened = (await File.ReadAllLinesAsync(Out("open.args"), TestContext.Current.CancellationToken)).Single();
        Expect.True(opened.EndsWith(".command", StringComparison.Ordinal));
        Expect.True(!File.Exists(opened), "the .command file is removed once Open returns");
        Expect.True(!Directory.Exists(Path.GetDirectoryName(opened)!), "its private folder is removed with it");
    }

    [Fact(Timeout = 30_000)]
    public async Task Linux_uses_xdg_terminal_exec_with_the_attach_argv_in_the_request_cwd()
    {
        await _f.StartSession();
        _ = RecordArgs("xdg-terminal-exec");
        _ = RecordArgs("x-terminal-emulator");
        var request = _f.Request();

        _ = await _f.Drive(Viewer(mac: false, "xdg-terminal-exec", "x-terminal-emulator").Open(request, TestContext.Current.CancellationToken));

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

        _ = await _f.Drive(Viewer(mac: false, "x-terminal-emulator").Open(request, TestContext.Current.CancellationToken));

        Expect.Sequence(["-e", .. request.AttachArgv], await File.ReadAllLinesAsync(Out("x-terminal-emulator.args"), TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 30_000)]
    public async Task Linux_with_neither_terminal_fails_naming_both_and_spawns_nothing()
    {
        await _f.StartSession();
        _ = RecordArgs("xdg-terminal-exec");
        _ = RecordArgs("x-terminal-emulator");

        var outcome = await _f.Drive(Viewer(mac: false).Open(_f.Request(), TestContext.Current.CancellationToken));

        Expect.Equal(ViewerOutcomeKind.Failed, outcome.Kind);
        Expect.Equal(SindriErrors.LaunchViewerFailed.Code, outcome.Code);
        Expect.True(outcome.Detail!.Contains("xdg-terminal-exec", StringComparison.Ordinal) && outcome.Detail.Contains("x-terminal-emulator", StringComparison.Ordinal));
        Expect.True(!File.Exists(Out("xdg-terminal-exec.args")) && !File.Exists(Out("x-terminal-emulator.args")));
    }

    [Fact(Timeout = 30_000)]
    public async Task Probe_names_what_is_missing()
    {
        var mac = await Viewer(mac: true).Probe(TestContext.Current.CancellationToken);
        var linux = await Viewer(mac: false, "x-terminal-emulator").Probe(TestContext.Current.CancellationToken);
        var none = await Viewer(mac: false).Probe(TestContext.Current.CancellationToken);

        Expect.Equal("default-terminal", mac.Kind);
        Expect.Equal("not-installed", mac.Problem);
        Expect.True(linux.Present);
        Expect.Equal("not-installed", none.Problem);
    }
}
