using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The box-local setup (API P10a, U7): the in-memory detection, <c lang="text">launch init</c> and <c lang="text">launch show</c> (launcher spec §4).</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LauncherInitTests : IDisposable
{
    private const string Aider = """{ "schema": 1, "agents": { "aider": { "label": "Aider", "bin": "aider", "open": ["{bin}"], "sessionId": "none" } } }""";
    private const string BinOnlyOverride = """{ "schema": 1, "agents": { "claude-code": { "override": true, "bin": "/opt/claude/bin/claude" } } }""";

    private readonly string _home = Directory.CreateTempSubdirectory("aw-init-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private string FilePath => Path.Combine(_home, "launcher.json");

    private ReloadingAgentCatalog Catalog() => LauncherStartup.Catalog(name => name == "AW_LAUNCHER_CONFIG" ? FilePath : null, new SindriSettings(_home, Path.Combine(_home, "profile.yaml")) { StateDirectory = _home });

    private static DetectionInputs Detection(Dictionary<string, string> env, params string[] onPath) =>
        new(name => env.GetValueOrDefault(name), (program, _) => ValueTask.FromResult(onPath.Contains(program, StringComparer.Ordinal)), _ => ValueTask.FromResult(false), TerminalTemplates.Load());

    private static readonly Dictionary<string, string> _gui = new(StringComparer.Ordinal) { ["DISPLAY"] = ":97" };

    private LauncherInit Setup(DetectionInputs detection, AgentSettings? approved = null) =>
        new(new LauncherInitParts(Catalog(), detection)
        {
            BuiltinHash = id => LauncherConfigFile.Read(LauncherStartup.Builtins(), user: null).Value.Agents.SingleOrDefault(a => a.Id.Equals(id))?.ArgvHash,
            Approved = _ => ValueTask.FromResult(approved),
            ResolveBin = (bin, _) => ValueTask.FromResult<string?>("/usr/bin/" + bin),
        });

    [Fact]
    public async Task Detection_under_fake_PATH_and_TERM_PROGRAM_and_no_gui()
    {
        var no_gui = await TerminalDetection.Detect(Detection(new Dictionary<string, string>(StringComparer.Ordinal) { ["TERM_PROGRAM"] = "kitty" }, "kitty"), CancellationToken.None);
        var term = await TerminalDetection.Detect(new DetectionInputs(n => n == "DISPLAY" ? ":97" : n == "TERM_PROGRAM" ? "kitty" : null, (_, _) => ValueTask.FromResult(false), _ => ValueTask.FromResult(false), TerminalTemplates.Load()), CancellationToken.None);
        var warp = await TerminalDetection.Detect(Detection(_gui, "kitty") with { WarpPresent = _ => ValueTask.FromResult(true) }, CancellationToken.None);
        var first = await TerminalDetection.Detect(Detection(_gui, "xterm", "kitty"), CancellationToken.None);
        var onlyXterm = await TerminalDetection.Detect(Detection(_gui, "xterm"), CancellationToken.None);
        var nothing = await TerminalDetection.Detect(Detection(_gui), CancellationToken.None);

        Expect.Equal("print", no_gui.Kind);
        Expect.Equal("kitty", term.Template?.Id);
        Expect.Equal("warp", warp.Kind);
        Expect.Equal("kitty", first.Template?.Id);   // terminals.json order, not PATH order
        Expect.Equal("xterm", onlyXterm.Template?.Id);
        Expect.Equal("print", nothing.Kind);
    }

    [Fact]
    public async Task Init_writes_a_0600_file_the_next_read_uses()
    {
        var report = (await Setup(Detection(_gui, "xterm")).Init(new LaunchInitRequest(Viewer: null, Force: false, ToStdout: false), CancellationToken.None)).Value;

        Expect.Equal(FilePath, report.Path);
        Expect.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(FilePath));
        var config = Catalog().Current;
        Expect.Equal("exec-template", config.Viewer?.Kind);
        Expect.Equal("xterm", config.Viewer?.Name);
        Expect.True(config.Problem is null && !config.Unusable, config.Problem ?? string.Empty);
    }

    [Fact]
    public async Task Init_refuses_an_existing_file_without_force_exit_1()
    {
        await File.WriteAllTextAsync(FilePath, Aider, TestContext.Current.CancellationToken);
        var setup = Setup(Detection(_gui, "xterm"));

        var refused = await setup.Init(new LaunchInitRequest(Viewer: null, Force: false, ToStdout: false), CancellationToken.None);
        var forced = await setup.Init(new LaunchInitRequest(Viewer: null, Force: true, ToStdout: false), CancellationToken.None);

        Expect.True(refused.IsError && refused.Error.Kind == ErrorKind.Conflict);
        Expect.True(forced.IsSuccess);
        Expect.Equal("xterm", Catalog().Current.Viewer?.Name);
    }

    [Fact]
    public async Task Init_stdout_writes_nothing()
    {
        var report = (await Setup(Detection(_gui, "xterm")).Init(new LaunchInitRequest(Viewer: null, Force: false, ToStdout: true), CancellationToken.None)).Value;

        Expect.True(report.Path is null && report.Json.Contains("exec-template", StringComparison.Ordinal));
        Expect.True(!File.Exists(FilePath));
    }

    [Fact]
    public async Task Init_viewer_flag_picks_print_or_a_template_and_refuses_an_unknown_name()
    {
        var setup = Setup(Detection(_gui, "xterm"));

        var print = await setup.Init(new LaunchInitRequest("print", Force: false, ToStdout: true), CancellationToken.None);
        var kitty = await setup.Init(new LaunchInitRequest("kitty", Force: false, ToStdout: true), CancellationToken.None);
        var unknown = await setup.Init(new LaunchInitRequest("nope", Force: false, ToStdout: true), CancellationToken.None);

        Expect.Equal("print", print.Value.Viewer);
        Expect.Equal("kitty", kitty.Value.Viewer);
        Expect.True(unknown.IsError && unknown.Error.Kind == ErrorKind.Invalid);
    }

    [Fact]
    public async Task Show_prints_will_park_line_for_unapproved_entries()
    {
        await File.WriteAllTextAsync(FilePath, Aider, TestContext.Current.CancellationToken);

        var report = (await Setup(Detection(_gui)).Show(only: null, CancellationToken.None)).Value;

        var aider = report.Entries.Single(e => e.Id.Value == "aider");
        Expect.True(!aider.StepOk);
        Expect.True(aider.ParkLine is { } line && line.StartsWith("Steps on `aider` will park: approve `agents.aider.stepArgvHash: sha256:", StringComparison.Ordinal) && line.EndsWith(aider.ArgvHash + "`", StringComparison.Ordinal));
        Expect.True(report.Entries.Where(e => e.Source == CatalogSource.Builtin).All(e => e.StepOk && e.ParkLine is null));
    }

    [Fact]
    public async Task Show_bin_only_override_has_no_park_line()
    {
        await File.WriteAllTextAsync(FilePath, BinOnlyOverride, TestContext.Current.CancellationToken);

        var report = (await Setup(Detection(_gui)).Show(AgentId.From("claude-code"), CancellationToken.None)).Value;

        var entry = report.Entries.Single();
        Expect.Equal(CatalogSource.Override, entry.Source);
        Expect.True(entry.StepOk && entry.ParkLine is null);
        Expect.Equal("/opt/claude/bin/claude", entry.Bin);
    }

    [Fact]
    public async Task Show_resolves_the_bin_and_an_approved_hash_is_ok()
    {
        await File.WriteAllTextAsync(FilePath, Aider, TestContext.Current.CancellationToken);
        var first = (await Setup(Detection(_gui)).Show(AgentId.From("aider"), CancellationToken.None)).Value.Entries.Single();
        var approved = new AgentSettings(Targets: new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { ["aider"] = new(StepArgvHash: [first.ArgvHash!]) });

        var report = (await Setup(Detection(_gui), approved).Show(only: null, CancellationToken.None)).Value;

        var aider = report.Entries.Single(e => e.Id.Value == "aider");
        Expect.True(aider.StepOk && aider.ParkLine is null);
        Expect.Equal("/usr/bin/aider", aider.ResolvedBin);
        Expect.Equal(CatalogSource.Added, aider.Source);
        Expect.Equal("detected", report.TerminalSource);
    }

    [Fact]
    public async Task Show_of_an_unknown_id_is_not_found()
    {
        var shown = await Setup(Detection(_gui)).Show(AgentId.From("ghost"), CancellationToken.None);

        Expect.True(shown.IsError && shown.Error.Kind == ErrorKind.NotFound);
    }
}
