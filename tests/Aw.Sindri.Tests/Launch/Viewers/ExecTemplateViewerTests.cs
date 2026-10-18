using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch.Viewers;

[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ExecTemplateViewerTests : TerminalViewerContract, IDisposable
{
    private static readonly string[] _template = ["{title}", "{cwd}", "{cmd...}"];
    private readonly ViewerFixture _f = new();

    public void Dispose() => _f.Dispose();

    protected override ITerminalViewer Create() => Viewer(_f.Script("quiet.sh", "exit 0"));

    // a zero window: the contract tests call Open without moving the fake clock, so no poll may wait on it
    protected override ViewerRequest Request() => _f.Request() with { Window = TimeSpan.Zero };

    // A stub script is run by "sh <file>", never exec'd: a script written a moment ago can still be open for writing in a process another test forked at that
    // instant, and exec of such a file fails with ETXTBSY ("Text file busy"), so the viewer answers 'failed' at once and the stub never runs (seen once in a gate lane).
    private ExecTemplateViewer Viewer(string script, Func<string, string?>? getEnv = null) =>
        new(new ExecTemplateOptions("stub", File.Exists(script) ? ["sh", script, .. _template] : [script, .. _template]) { GetEnv = getEnv }, _f.Host, _f.Clock);

    private static string? Display(string name) => name == "DISPLAY" ? ":99" : null;

    [Fact(Timeout = 30_000)]
    public async Task Nonzero_exit_in_window_is_failed_with_redacted_stderr()
    {
        await _f.StartSession();
        var token = "gh" + "p_" + new string('a', 36);   // looks like a GitHub token, is not one
        var viewer = Viewer(_f.Script("fail.sh", $"echo 'no display; token {token}' >&2; exit 1"));

        var outcome = await _f.Drive(viewer.Open(_f.Request(), TestContext.Current.CancellationToken));

        Expect.Equal(ViewerOutcomeKind.Failed, outcome.Kind);
        Expect.Equal(SindriErrors.LaunchViewerFailed.Code, outcome.Code);
        Expect.True(outcome.Detail!.Contains("no display", StringComparison.Ordinal));
        Expect.True(!outcome.Detail.Contains(token, StringComparison.Ordinal));
    }

    [Fact(Timeout = 30_000)]
    public async Task The_terminal_runs_in_its_own_session_with_stdin_and_stdout_on_dev_null()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() && File.Exists("/usr/bin/setsid"), "needs /proc and setsid");
        await _f.StartSession();
        var seen = Path.Combine(_f.Dir, "seen.txt");
        var viewer = Viewer(_f.Script("detached.sh", $"in=$(readlink /proc/$$/fd/0); out=$(readlink /proc/$$/fd/1); sid=$(ps -o sid= -p $$); printf '%s\\n%s\\n%s\\n%s\\n' \"$in\" \"$out\" \"$sid\" \"$$\" > '{seen}'; exit 0"));

        _ = await _f.Drive(viewer.Open(_f.Request() with { Window = TimeSpan.Zero }, TestContext.Current.CancellationToken));
        await WaitFor(seen);

        var lines = (await File.ReadAllLinesAsync(seen, TestContext.Current.CancellationToken)).Select(static l => l.Trim()).ToArray();
        Expect.Equal(("/dev/null", "/dev/null"), (lines[0], lines[1]));
        Expect.Equal(lines[3], lines[2]);   // session id = its own pid: a session leader, so Ctrl-C on a foreground Sindri doesn't reach it
    }

    private static async Task WaitFor(string path)
    {
        for (var i = 0; i < 200 && (!File.Exists(path) || new FileInfo(path).Length < 20); i++)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task The_kept_stderr_is_at_most_2_KB()
    {
        await _f.StartSession();
        var viewer = Viewer(_f.Script("noisy.sh", "head -c 20000 /dev/zero | tr '\\0' 'x' >&2; exit 2"));

        var outcome = await _f.Drive(viewer.Open(_f.Request(), TestContext.Current.CancellationToken));

        Expect.Equal(ViewerOutcomeKind.Failed, outcome.Kind);
        Expect.True(outcome.Detail!.Length is > 0 and <= 2048);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_client_in_window_is_opened()
    {
        await _f.StartSession();
        var viewer = Viewer(_f.Script("stay.sh", "sleep 2"));

        var outcome = await _f.Drive(viewer.Open(_f.Request(), TestContext.Current.CancellationToken), tick =>
        {
            if (tick == 2)
            {
                _f.Host.AttachClient(_f.Session);
            }
        });

        Expect.Equal(ViewerOutcome.Opened, outcome);
    }

    [Fact(Timeout = 30_000)]
    public async Task No_client_is_unconfirmed()
    {
        await _f.StartSession();
        var viewer = Viewer(_f.Script("stay.sh", "sleep 2"));

        var outcome = await _f.Drive(viewer.Open(_f.Request(), TestContext.Current.CancellationToken));

        Expect.Equal(ViewerOutcome.Unconfirmed, outcome);
    }

    [Fact(Timeout = 30_000)]
    public async Task Exit_0_without_client_is_unconfirmed_never_opened()
    {
        await _f.StartSession();
        var viewer = Viewer(_f.Script("gone.sh", "exit 0"));

        var outcome = await _f.Drive(viewer.Open(_f.Request(), TestContext.Current.CancellationToken));

        Expect.Equal(ViewerOutcome.Unconfirmed, outcome);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_missing_program_is_failed_001()
    {
        await _f.StartSession();
        var viewer = new ExecTemplateViewer(new ExecTemplateOptions("stub", [Path.Combine(_f.Dir, "no-such-terminal")]), _f.Host, _f.Clock);

        var outcome = await _f.Drive(viewer.Open(_f.Request(), TestContext.Current.CancellationToken));

        Expect.Equal(ViewerOutcomeKind.Failed, outcome.Kind);
        Expect.Equal(SindriErrors.LaunchViewerFailed.Code, outcome.Code);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_title_that_looks_like_an_option_is_refused_before_the_spawn()
    {
        await _f.StartSession();
        var marker = Path.Combine(_f.Dir, "ran.txt");
        var viewer = Viewer(_f.Script("mark.sh", $"echo ran > '{marker}'"));

        var outcome = await _f.Drive(viewer.Open(_f.Request("-e evil"), TestContext.Current.CancellationToken));

        Expect.Equal(ViewerOutcomeKind.Failed, outcome.Kind);
        Expect.Equal(SindriErrors.LaunchViewerFailed.Code, outcome.Code);
        Expect.True(!File.Exists(marker));
    }

    [Fact(Timeout = 30_000)]
    public async Task No_secret_or_item_text_in_argv_or_files()
    {
        await _f.StartSession();
        var argvFile = Path.Combine(_f.Dir, "argv.txt");
        var envFile = Path.Combine(_f.Dir, "env.txt");
        var doneFile = Path.Combine(_f.Dir, "done.txt");   // written last: until it exists the stub is still running, so the window must not end
        var viewer = Viewer(_f.Script("rec.sh", $"for a in \"$@\"; do printf '%s\\n' \"$a\"; done > '{argvFile}'; env > '{envFile}'; : > '{doneFile}'"));
        var previous = Environment.GetEnvironmentVariable("TMUX");
        Environment.SetEnvironmentVariable("TMUX", "/tmp/not-ours,1,0");
        try
        {
            _ = await _f.Drive(viewer.Open(_f.Request(), TestContext.Current.CancellationToken), hold: () => !File.Exists(doneFile));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TMUX", previous);
        }

        var recorded = await File.ReadAllLinesAsync(argvFile, TestContext.Current.CancellationToken);
        Expect.Sequence(["sindri ITM-1", _f.Dir, .. _f.Host.AttachArgv(_f.Session)], recorded);   // the title, the repo path and the attach command, nothing else
        Expect.True(!(await File.ReadAllTextAsync(envFile, TestContext.Current.CancellationToken)).Contains("TMUX=", StringComparison.Ordinal));
        Expect.Sequence(["argv.txt", "done.txt", "env.txt", "rec.sh"], Directory.GetFiles(_f.Dir).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal));
    }

    [Fact(Timeout = 30_000)]
    public async Task The_probe_finds_a_program_on_the_login_path()
    {
        var facts = await Viewer("sh", Display).Probe(TestContext.Current.CancellationToken);

        Expect.True(facts.Present);
        Expect.Equal("exec-template", facts.Kind);
        Expect.Equal("stub", facts.Label);
        Expect.True(null == facts.Problem);
    }

    [Fact(Timeout = 30_000)]
    public async Task The_probe_names_a_missing_program()
    {
        var facts = await Viewer("no-such-terminal-program", Display).Probe(TestContext.Current.CancellationToken);

        Expect.True(!facts.Present);
        Expect.Equal("not-installed", facts.Problem);
    }

    [Fact(Timeout = 30_000)]
    public async Task The_probe_names_a_missing_gui_session()
    {
        var facts = await Viewer("sh", _ => null).Probe(TestContext.Current.CancellationToken);

        Expect.True(!facts.Present);
        Expect.Equal("no-gui-session", facts.Problem);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_template_without_title_is_noted_in_the_probe_detail()
    {
        var viewer = new ExecTemplateViewer(new ExecTemplateOptions("stub", ["sh", "{cmd...}"]) { GetEnv = Display }, _f.Host, _f.Clock);

        var facts = await viewer.Probe(TestContext.Current.CancellationToken);

        Expect.True(facts.Present && facts.Detail!.Reason.Contains("{title}", StringComparison.Ordinal));
    }
}
