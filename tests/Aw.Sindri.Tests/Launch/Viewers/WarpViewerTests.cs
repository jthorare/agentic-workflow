using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using YamlDotNet.RepresentationModel;

namespace Aw.Sindri.Tests.Launch.Viewers;

[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class WarpViewerTests : TerminalViewerContract, IDisposable
{
    private readonly ViewerFixture _f = new();
    private readonly string _launchDir;

    public WarpViewerTests()
    {
        _launchDir = Path.Combine(_f.Dir, "launch_configurations");
    }

    public void Dispose() => _f.Dispose();

    protected override ITerminalViewer Create() => Viewer(_f.Script("open-ok.sh", "exit 0"));

    // a zero window: the contract tests call Open without moving the fake clock, so no poll may wait on it
    protected override ViewerRequest Request() => _f.Request() with { Window = TimeSpan.Zero };

    private WarpViewer Viewer(string opener, Func<string, string?>? getEnv = null) =>
        new(new WarpViewerOptions(_launchDir) { Opener = opener, GetEnv = getEnv }, _f.Host, _f.Clock);

    private string LaunchFile => Path.Combine(_launchDir, "sindri-snd-one.yaml");

    [Fact(Timeout = 30_000)]
    public async Task Open_writes_a_0600_launch_file_and_opens_its_uri()
    {
        await _f.StartSession();
        var uriFile = Path.Combine(_f.Dir, "uri.txt");
        var viewer = Viewer(_f.Script("open-rec.sh", $"printf '%s' \"$1\" > '{uriFile}'; test $# -eq 1"));

        _ = await _f.Drive(viewer.Open(_f.Request(), TestContext.Current.CancellationToken));

        Expect.Equal("warp://launch/sindri-snd-one.yaml", await File.ReadAllTextAsync(uriFile, TestContext.Current.CancellationToken));
        Expect.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(LaunchFile));
    }

    [Fact(Timeout = 30_000)]
    public async Task The_launch_file_holds_the_title_the_path_and_one_quoted_exec_line()
    {
        await _f.StartSession();
        var request = _f.Request() with { AttachArgv = ["tmux", "-L", "it's", "attach-session", "-t", "=snd-one"] };

        _ = await _f.Drive(Create().Open(request, TestContext.Current.CancellationToken));

        var text = await File.ReadAllTextAsync(LaunchFile, TestContext.Current.CancellationToken);
        var yaml = new YamlStream();
        yaml.Load(new StringReader(text));
        var scalars = ((YamlMappingNode)yaml.Documents[0].RootNode).AllNodes.OfType<YamlScalarNode>().Select(n => n.Value).ToList();
        Expect.True(scalars.Contains("sindri ITM-1") && scalars.Contains(_f.Dir));
        Expect.True(scalars.Contains("tmux -L 'it'\\''s' attach-session -t '=snd-one'"));
        Expect.True(!text.Contains("AW_", StringComparison.Ordinal));   // no environment, no secret, no item text beyond the title
    }

    [Fact(Timeout = 30_000)]
    public async Task A_refused_uri_is_failed_001_with_the_openers_stderr()
    {
        await _f.StartSession();
        var viewer = Viewer(_f.Script("open-bad.sh", "echo 'no handler for warp://' >&2; exit 3"));

        var outcome = await _f.Drive(viewer.Open(_f.Request(), TestContext.Current.CancellationToken));

        Expect.Equal(ViewerOutcomeKind.Failed, outcome.Kind);
        Expect.Equal(SindriErrors.LaunchViewerFailed.Code, outcome.Code);
        Expect.True(outcome.Detail!.Contains("no handler", StringComparison.Ordinal));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_client_in_window_is_opened()
    {
        await _f.StartSession();

        var outcome = await _f.Drive(Create().Open(_f.Request(), TestContext.Current.CancellationToken), tick =>
        {
            if (tick == 1)
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

        var outcome = await _f.Drive(Create().Open(_f.Request(), TestContext.Current.CancellationToken));

        Expect.Equal(ViewerOutcome.Unconfirmed, outcome);
    }

    [Fact(Timeout = 30_000)]
    public async Task Per_launch_files_are_0600_and_swept()
    {
        await _f.StartSession();
        _ = await _f.Drive(Create().Open(_f.Request(), TestContext.Current.CancellationToken));
        Expect.True(File.Exists(LaunchFile));

        _f.Clock.Advance(TimeSpan.FromSeconds(59));
        Expect.True(File.Exists(LaunchFile));
        _f.Clock.Advance(TimeSpan.FromSeconds(2));

        Expect.True(!File.Exists(LaunchFile));
    }

    [Fact]
    public void The_start_up_sweep_removes_only_stale_launch_files()
    {
        _ = Directory.CreateDirectory(_launchDir);
        var stale = Path.Combine(_launchDir, "sindri-snd-old.yaml");
        var fresh = Path.Combine(_launchDir, "sindri-snd-new.yaml");
        var other = Path.Combine(_launchDir, "mine.yaml");
        foreach (var path in new[] { stale, fresh, other })
        {
            File.WriteAllText(path, "name: x\n");
        }

        var now = _f.Clock.GetUtcNow().UtcDateTime;
        File.SetLastWriteTimeUtc(stale, now - TimeSpan.FromMinutes(2));
        File.SetLastWriteTimeUtc(other, now - TimeSpan.FromMinutes(2));
        File.SetLastWriteTimeUtc(fresh, now - TimeSpan.FromSeconds(10));

        Viewer("true").Sweep();

        Expect.True(!File.Exists(stale) && File.Exists(fresh) && File.Exists(other));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_host_name_outside_the_id_pattern_is_refused_and_writes_nothing()
    {
        var request = _f.Request() with { Host = new HostRef(HostKind.Tmux, "../escape") };

        var outcome = await _f.Drive(Create().Open(request, TestContext.Current.CancellationToken));

        Expect.Equal(ViewerOutcomeKind.Failed, outcome.Kind);
        Expect.True(!Directory.Exists(_launchDir) || Directory.GetFiles(_launchDir).Length == 0);
    }

    [Fact(Timeout = 30_000)]
    public async Task The_probe_needs_a_gui_session_and_an_opener()
    {
        var noGui = await Viewer("sh", _ => null).Probe(TestContext.Current.CancellationToken);
        var missing = await Viewer("no-such-opener", n => n == "DISPLAY" ? ":99" : null).Probe(TestContext.Current.CancellationToken);

        Expect.Equal("no-gui-session", noGui.Problem);
        Expect.True(!missing.Present);
        Expect.Equal("warp", missing.Kind);
    }
}
