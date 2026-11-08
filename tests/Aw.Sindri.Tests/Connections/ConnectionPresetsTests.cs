using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Infrastructure.Connections;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class ConnectionPresetsTests
{
    private static readonly string[] _shipped = ["github", "google", "linear", "slack", "spotify"];

    [Fact]
    public void Every_shipped_preset_parses_and_the_five_are_there()
    {
        var presets = new EmbeddedConnectionPresets().List();

        Assert.True(presets.IsSuccess);
        Assert.Equal(_shipped, presets.Value.Select(static p => p.Key), StringComparer.Ordinal);
    }

    [Fact]
    public void No_shipped_preset_carries_a_client_id_or_a_secret()
    {
        var files = Directory.GetFiles(PresetsFolder(), "*.json");

        Assert.Equal(_shipped.Length, files.Length);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("client", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Every_endpoint_is_https_and_every_preset_is_an_oauth_one_with_scopes_and_a_note()
    {
        foreach (var preset in new EmbeddedConnectionPresets().List().Value)
        {
            Assert.Equal(ConnectionKind.OAuth, preset.Kind);
            Assert.StartsWith("https://", preset.AuthorizeUrl, StringComparison.Ordinal);
            Assert.StartsWith("https://", preset.TokenUrl, StringComparison.Ordinal);
            Assert.True(preset.RevokeUrl is null || preset.RevokeUrl.StartsWith("https://", StringComparison.Ordinal), preset.Key);
            Assert.True(preset.ProbeUrl is null || preset.ProbeUrl.StartsWith("https://", StringComparison.Ordinal), preset.Key);
            Assert.NotEmpty(preset.DefaultScopes);
            Assert.NotEmpty(preset.HostGroup);
            Assert.NotEmpty(preset.SignInNote);
            Assert.Empty(preset.PollableEvents);
        }
    }

    [Fact]
    public void A_provider_that_refuses_a_plain_http_loopback_says_how_to_sign_in_instead()
    {
        foreach (var preset in new EmbeddedConnectionPresets().List().Value.Where(static p => !p.LoopbackAllowed))
        {
            Assert.Contains("--kind rest", preset.SignInNote, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Find_answers_a_shipped_key_and_null_for_another()
    {
        var catalog = new EmbeddedConnectionPresets();

        Assert.Equal("GitHub", catalog.Find("github")?.DisplayName);
        Assert.Null(catalog.Find("myspace"));
    }

    [Fact]
    public void A_preset_with_a_missing_key_or_a_plain_http_endpoint_does_not_parse()
    {
        const string NoToken = """{"id":"x","displayName":"X","kind":"oauth","authorizeUrl":"https://x.test/a","defaultScopes":["a"],"hostGroup":"x","loopbackAllowed":true,"signInNote":"n","pollableEvents":[]}""";
        const string PlainHttp = """{"id":"x","displayName":"X","kind":"oauth","authorizeUrl":"http://x.test/a","tokenUrl":"https://x.test/t","defaultScopes":["a"],"hostGroup":"x","loopbackAllowed":true,"signInNote":"n","pollableEvents":[]}""";

        Assert.True(ConnectionPresets.Parse(NoToken).IsError);
        Assert.True(ConnectionPresets.Parse(PlainHttp).IsError);
        Assert.True(ConnectionPresets.Parse("not json").IsError);
    }

    private static string PresetsFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "config", "connections", "presets")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("config/connections/presets"), "config", "connections", "presets");
    }
}
