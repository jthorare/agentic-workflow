using System.Text;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Core.Images;
using Aw.Evidence.Features.DesktopRun;
using Aw.Evidence.Testing;
using Aw.Kernel;
using Aw.Kernel.Services;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Evidence.Tests.Desktop;

/// <summary>
/// The desktop-run slice over in-memory fakes. The repo's config names a fixture ('fx up', then 'fx items'), an app and a driver program; the
/// driver's screenshot "writes" each frame by putting a 40x20 white image where --out says, and the browser's design frame is the same white image,
/// so a default run matches pixel for pixel. Two screens, 'home' and 'agents', at the default sizes would be four launches: the config narrows them to one size.
/// </summary>
internal sealed class DesktopRunHarness
{
    internal const string Repo = "/work/repo";
    internal const string ManifestPath = "/work/repo/docs/screens.json";
    internal const string RunDirectory = "/runs/desktop-1";
    internal static readonly string Config = """
        {"desktop":{"manifest":"docs/screens.json","homeEnv":"APP_HOME",
          "fixture":{"command":["scripts/fx.sh"],"up":[["up"]],"down":["down"]},
          "app":{"command":["bin/app","--flag"]},"driver":{"command":["bin/aw-ui-driver"]},
          "sizes":["40x20"],"env":{"APP_CLI":"bin/app"}}}
        """.ReplaceLineEndings("\n");

    internal static readonly string Manifest = """
        {"schema":"ui-evidence.desktop-screens/1","designRoot":"mockups","screens":[
          {"name":"home","design":"home.html","fixture":[["items"]],"accepted":["clock text"],"steps":[
            {"action":"launch"},
            {"action":"click","target":{"automationId":"nav-home"},"expectedState":{"state":"visible"}},
            {"action":"wait-for","target":{"text":"Home"},"expectedState":{"state":"textContains","text":"Hom"}},
            {"action":"screenshot"}]},
          {"name":"agents","design":"agents.html","fixture":[["items"]],"threshold":2,"steps":[
            {"action":"click","target":{"name":"Agents"}},
            {"action":"screenshot"}]}]}
        """.ReplaceLineEndings("\n");

    internal DesktopRunHarness()
    {
        Browser.PageFor = _ => Page;
        Page.Screenshot = Codec.Encode(RgbaImage.Filled(40, 20, 0xFFFFFFFF));
        Targets = new FakeEvidenceTargets(TestJson.Options).File(Repo + "/.agentic-workflow/evidence.json", Config);
        _ = Files.With(ManifestPath, Encoding.UTF8.GetBytes(Manifest));
        _ = Commands.When(IsShot, WriteFrame);
    }

    internal DesktopRunHarness WithoutDesktopConfig()
    {
        _ = Targets.File(Repo + "/.agentic-workflow/evidence.json", "{}");
        return this;
    }

    internal InMemoryRunFiles Files { get; } = new();

    internal InMemoryMachineLocks Locks { get; } = new();

    internal FakeDesktopCommands Commands { get; } = new();

    internal FakeScratchDirectories Scratch { get; } = new();

    internal FakeDesktopApps Apps { get; } = new();

    internal FakeStaticSite Site { get; } = new();

    internal FakeBrowser Browser { get; } = new();

    internal FakePage Page { get; } = new();

    internal FakeImageCodec Codec { get; } = new();

    internal FakeEvidenceTargets Targets { get; }

    internal FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));

    internal static string SummaryPath => RunDirectory + "/summary.json";

    internal static DesktopRunRequest Request(IReadOnlyList<string>? screens = null) =>
        new(ManifestPath, RunDirectory, Repo, TimeSpan.Zero, AppBuild: "abc123", Screens: screens);

    internal ValueTask<Result<DesktopRunResponse>> Run(DesktopRunRequest? request = null, CancellationToken cancellationToken = default)
    {
        var files = new DataRunFiles(Files, new FakeTextScrubber(), TestJson.Options);
        var service = new ValidatingService<DesktopRunRequest, DesktopRunResponse>(
            new DesktopRunService(
                files, new DesktopPlans(Targets, TestJson.Options), Locks, new DesktopStack(Commands, Scratch, new DesktopDriving(Apps, Commands, Scratch, Files)),
                new DesktopFrames(Browser, Site, Files, Codec), Clock),
            new DesktopRunValidator());
        return service.Handle(request ?? Request(), cancellationToken);
    }

    // The driver's screenshot verb: wherever --out says, a 40x20 white image (what the app would render).
    internal static bool IsShot(CommandSpec command) => command.Argv[0].EndsWith("aw-ui-driver", StringComparison.Ordinal) && command.Argv.Contains("screenshot", StringComparer.Ordinal);

    private CommandResult WriteFrame(CommandSpec command)
    {
        _ = Files.With(command.Argv[command.Argv.ToList().IndexOf("--out") + 1], Codec.Encode(AppFrame));
        return new CommandResult(0, "{\"path\":\"x\"}", string.Empty);
    }

    internal RgbaImage AppFrame { get; set; } = RgbaImage.Filled(40, 20, 0xFFFFFFFF);
}
