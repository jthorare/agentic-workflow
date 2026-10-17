using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.Config;

/// <summary>A launcher.json that does not parse (launcher L-3, L-4): the probe names the file and the line, and the viewer is not usable.</summary>
[Lane(TestLane.Unit)]
public sealed class ConfigInvalidProbeTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-invalid-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    [Fact]
    public async Task Config_invalid_carries_line_and_path()
    {
        var path = Path.Combine(_home, "launcher.json");
        await File.WriteAllTextAsync(path, "{\n  \"schema\": 1,\n  \"viewer\": {\n}\n  oops\n", TestContext.Current.CancellationToken);
        var catalog = new ReloadingAgentCatalog(path, LauncherStartup.Builtins());
        var detection = new DetectionInputs(static _ => null, static (_, _) => ValueTask.FromResult(false), static _ => ValueTask.FromResult(false), []);
        using var viewer = new ConfiguredViewer(catalog, static _ => new PrintViewer(), detection);

        var facts = await viewer.Probe(TestContext.Current.CancellationToken);

        Expect.Equal(("config-invalid", path), (facts.Problem, facts.ConfigPath));
        Assert.Matches(@"^line \d+: ", facts.Detail);
    }

    [Fact]
    public async Task A_missing_file_has_its_path_but_no_problem()
    {
        var path = Path.Combine(_home, "launcher.json");
        var catalog = new ReloadingAgentCatalog(path, LauncherStartup.Builtins());
        var detection = new DetectionInputs(static _ => null, static (_, _) => ValueTask.FromResult(false), static _ => ValueTask.FromResult(false), []);
        using var viewer = new ConfiguredViewer(catalog, static _ => new PrintViewer(), detection);

        var facts = await viewer.Probe(TestContext.Current.CancellationToken);

        Expect.Equal(("detected", path), (facts.Source, facts.ConfigPath));
        Assert.Null(facts.Problem);
    }
}
