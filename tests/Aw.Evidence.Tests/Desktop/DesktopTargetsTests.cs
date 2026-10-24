using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Tests.Desktop;

public sealed class DesktopTargetsTests
{
    private const string Fixture = "\"fixture\":{\"command\":[\"scripts/fx.sh\"],\"up\":[[\"up\"],[\"items\",\"--large\"]],\"down\":[\"down\"]}";
    private const string App = "\"app\":{\"command\":[\"bin/app\",\"--flag\"]}";

    private static string Config(string desktop) => "{\"desktop\":{" + desktop + "}}";

    private static Result<ResolvedTargets> Read(string json) => TargetsReader.Read(TestJson.Utf8(json), TestJson.Options);

    [Fact, Trait("Lane", "unit")]
    public void A_desktop_part_reads_with_its_defaults_filled_in()
    {
        var desktop = Read(Config($"\"manifest\":\"docs/screens.json\",\"homeEnv\":\"APP_HOME\",{Fixture},{App}")).Value.Desktop!;

        Assert.Equal(("docs/screens.json", "APP_HOME", 10d, TimeSpan.FromSeconds(300)), (desktop.Manifest, desktop.HomeEnv, desktop.Threshold, desktop.Timeout));
        Assert.Equal(["1440x900", "900x900"], desktop.Sizes.Select(static s => s.Describe()), StringComparer.Ordinal);
        Assert.Equal(["up", "items --large"], desktop.Fixture.Up.Select(static step => string.Join(' ', step)), StringComparer.Ordinal);
        Assert.Equal(["down"], desktop.Fixture.Down);
        Assert.Equal(["bin/app", "--flag"], desktop.App.Command);
        Assert.Equal(["aw-ui-driver"], desktop.DriverCommand);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_driver_command_of_the_repos_own_is_taken_as_given()
    {
        var desktop = Read(Config($"\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",{Fixture},{App},\"driver\":{{\"command\":[\"tools/driver.sh\",\"--quiet\"]}}")).Value.Desktop!;

        Assert.Equal(["tools/driver.sh", "--quiet"], desktop.DriverCommand);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_sizes_the_threshold_the_timeout_and_the_env_are_taken_as_given()
    {
        var desktop = Read(Config($"\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",{Fixture},{App},\"sizes\":[\"1200x800\"],\"threshold\":5,\"timeoutSeconds\":60,\"env\":{{\"APP_CLI\":\"bin/app\"}}")).Value.Desktop!;

        Assert.Equal(("1200x800", 5d, TimeSpan.FromSeconds(60), "bin/app"), (Assert.Single(desktop.Sizes).Describe(), desktop.Threshold, desktop.Timeout, desktop.Env["APP_CLI"]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_config_without_a_desktop_part_has_none() => Assert.Null(Read("{}").Value.Desktop);

    [Theory, Trait("Lane", "unit")]
    [InlineData("\"manifest\":\"/abs.json\",\"homeEnv\":\"APP_HOME\",FX,APPCMD", "$.desktop.manifest: must be a file path below the repo")]
    [InlineData("\"manifest\":\"../s.json\",\"homeEnv\":\"APP_HOME\",FX,APPCMD", "$.desktop.manifest")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"home\",FX,APPCMD", "$.desktop.homeEnv: must be an UPPER_SNAKE")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",FX,\"app\":{\"command\":[]}", "$.desktop.app.command: must list a program")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",FX,APPCMD,\"driver\":{\"command\":[\"\"]}", "$.desktop.driver.command: must list a program")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",\"fixture\":{\"command\":[\"fx\"],\"up\":[]},APPCMD", "$.desktop.fixture.up: must list at least one step")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",FX,APPCMD,\"sizes\":[\"big\"]", "'big' is not a size")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",FX,APPCMD,\"threshold\":120", "$.desktop.threshold: must be 0 to 100")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",FX,APPCMD,\"timeoutSeconds\":0", "$.desktop.timeoutSeconds")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",FX,APPCMD,\"env\":{\"lower\":\"x\"}", "$.desktop.env.lower: the name must be UPPER_SNAKE")]
    public void A_desktop_part_that_breaks_a_rule_is_refused_with_the_rule_named(string desktop, string expected)
    {
        var read = Read(Config(desktop.Replace("FX", Fixture, StringComparison.Ordinal).Replace("APPCMD", App, StringComparison.Ordinal)));

        Assert.Equal(EvidenceErrors.TargetsInvalid.Code, read.Error.Code);
        Assert.Contains(expected, read.Error.Message, StringComparison.Ordinal);
    }
}
