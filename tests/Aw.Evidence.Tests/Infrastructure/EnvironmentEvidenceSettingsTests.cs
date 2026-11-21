using Aw.Evidence.Core.Stack;
using Aw.Evidence.Infrastructure.Settings;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The deployment settings read from the environment and, for the app title, a local env file.</summary>
[Lane(TestLane.Integration)]
public sealed class EnvironmentEvidenceSettingsTests : IDisposable
{
    private readonly TempDirectory _root = new();

    [Fact]
    public async Task An_unconfigured_install_has_the_defaults_and_no_app_title()
    {
        var settings = new EnvironmentEvidenceSettings(Env(("UI_EVIDENCE_USER_AGENT", "  ")));

        Assert.Null(settings.UserAgent);
        Assert.Equal("judge-default", settings.VisualModel);
        Assert.Equal(SeedPolicy.Default, settings.SeedPolicy);
        Assert.Null(settings.PreviewHostPattern);
        Assert.Null(settings.ApiPort);
        Assert.Null(await settings.AppTitleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_preview_host_pattern_and_api_port_come_from_their_variables()
    {
        var settings = new EnvironmentEvidenceSettings(Env(("UI_EVIDENCE_PREVIEW_HOST_PATTERN", " pr-[0-9]+\\.preview\\.example\\.com "), ("UI_EVIDENCE_API_PORT", "5002")));

        Assert.Equal(("pr-[0-9]+\\.preview\\.example\\.com", 5002), (settings.PreviewHostPattern, settings.ApiPort));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData("50o2")]
    [InlineData(" ")]
    public void An_api_port_outside_1_to_65535_is_unset(string value) =>
        Assert.Null(new EnvironmentEvidenceSettings(Env(("UI_EVIDENCE_API_PORT", value))).ApiPort);

    [Fact]
    public async Task Each_setting_comes_from_its_variable()
    {
        var settings = new EnvironmentEvidenceSettings(Env(
            ("UI_EVIDENCE_USER_AGENT", "Agent/1"), ("AW_VISUAL_MODEL", "haiku"),
            ("UI_EVIDENCE_SEED_EMAIL_PATTERN", "seed-%@seed.test"), ("UI_EVIDENCE_APP_TITLE", "App")));

        Assert.Equal(("Agent/1", "haiku"), (settings.UserAgent, settings.VisualModel));
        Assert.Equal("seed-%@seed.test", settings.SeedPolicy.EmailPattern);
        Assert.Equal("App", await settings.AppTitleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Without_the_variable_the_app_title_comes_from_the_env_file_the_config_variable_names()
    {
        var file = _root.Combine("ui-evidence.local.env");
        await File.WriteAllTextAsync(file, "# local\nexport UI_EVIDENCE_APP_TITLE=\"My App\"\n", TestContext.Current.CancellationToken);

        var settings = new EnvironmentEvidenceSettings(Env(("UI_EVIDENCE_CONFIG", file)));
        var missing = new EnvironmentEvidenceSettings(Env(("UI_EVIDENCE_CONFIG", _root.Combine("absent.env"))));

        Assert.Equal("My App", await settings.AppTitleAsync(TestContext.Current.CancellationToken));
        Assert.Null(await missing.AppTitleAsync(TestContext.Current.CancellationToken));
        Assert.Contains("UI_EVIDENCE_CONFIG", settings.AppTitleSource, StringComparison.Ordinal);
    }

    public void Dispose() => _root.Dispose();

    private static Func<string, string?> Env(params (string Name, string Value)[] values) =>
        name => values.FirstOrDefault(v => v.Name == name).Value;
}
