using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
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

    // first-run spec decision 15a: only github and linear ship Sindri's own OAuth app id (a client id is not a secret); no preset ships a secret
    [Fact]
    public void No_shipped_preset_carries_a_secret_and_only_github_and_linear_carry_a_client_id()
    {
        var files = Directory.GetFiles(PresetsFolder(), "*.json");

        Assert.Equal(_shipped.Length, files.Length);
        foreach (var file in files)
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            var keys = json.RootElement.EnumerateObject().Select(static p => p.Name).ToList();
            var name = Path.GetFileNameWithoutExtension(file);
            string[] clientKeys = string.Equals(name, "github", StringComparison.Ordinal) || string.Equals(name, "linear", StringComparison.Ordinal) ? ["clientId"] : [];
            Assert.DoesNotContain(keys, static k => k.Contains("secret", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(clientKeys, keys.Where(static k => k.Contains("client", StringComparison.OrdinalIgnoreCase)), StringComparer.Ordinal);
        }
    }

    [Fact]
    public void The_github_preset_has_the_device_endpoint_and_ships_no_client_id_until_the_owner_registers_the_app()
    {
        var github = new EmbeddedConnectionPresets().Find("github");

        Assert.NotNull(github);
        Assert.Equal(("https://github.com/login/device/code", string.Empty), (github.DeviceCodeUrl, github.ShippedClient));
    }

    [Fact]
    public void A_plain_http_device_endpoint_is_refused()
    {
        var parsed = ConnectionPresets.Parse("""
            {"id":"x","displayName":"X","kind":"oauth","authorizeUrl":"https://x/a","tokenUrl":"https://x/t","defaultScopes":[],"hostGroup":"x","loopbackAllowed":true,"signInNote":"n","deviceCodeUrl":"http://x/d"}
            """);

        Assert.True(parsed.IsError);
        Assert.Contains("deviceCodeUrl", parsed.Error.Message, StringComparison.Ordinal);
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
            Assert.Equal(PollCatalog.For(preset.Key).Select(static k => k.Key), preset.PollableEvents, StringComparer.Ordinal);   // one list, not a copy in the JSON
        }
    }

    [Fact]
    public void A_provider_that_refuses_a_plain_http_loopback_says_how_to_sign_in_instead()
    {
        foreach (var preset in new EmbeddedConnectionPresets().List().Value.Where(static p => !p.LoopbackAllowed))
        {
            Assert.Contains($"--preset {preset.Key} --key-env", preset.SignInNote, StringComparison.Ordinal);
        }
    }

    // #4357: a key first where the service has one (Sindri runs on loopback, so an OAuth app has no good callback); OAuth only where there is no key
    [Fact]
    public void GitHub_Linear_and_Slack_take_a_key_first_and_Google_and_Spotify_sign_in_only()
    {
        var auth = new EmbeddedConnectionPresets().List().Value.ToDictionary(static p => p.Key, static p => string.Join(',', p.Auth), StringComparer.Ordinal);

        Assert.Equal(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["github"] = "key,oauth", ["google"] = "oauth", ["linear"] = "key,oauth", ["slack"] = "key", ["spotify"] = "oauth" },
            auth);
    }

    [Fact]
    public void A_preset_without_auth_signs_in_only_and_a_bad_auth_list_does_not_parse()
    {
        const string Head = """{"id":"x","displayName":"X","kind":"oauth","authorizeUrl":"https://x.test/a","tokenUrl":"https://x.test/t","defaultScopes":["a"],"hostGroup":"x","loopbackAllowed":true,"signInNote":"n" """;

        var bare = ConnectionPresets.Parse(Head + "}").Value;
        var keyed = ConnectionPresets.Parse(Head + ",\"auth\":[\"key\",\"oauth\"]}").Value;

        Assert.Equal(["oauth"], bare.Auth, StringComparer.Ordinal);
        Assert.False(bare.KeyFirst);
        Assert.True(keyed.KeyFirst);
        foreach (var bad in (string[])["[]", "[\"password\"]", "[\"key\",\"key\"]", "\"key\""])
        {
            var parsed = ConnectionPresets.Parse(Head + ",\"auth\":" + bad + "}");
            Assert.True(parsed.IsError, bad);
            Assert.Contains("'auth'", parsed.Error.Message, StringComparison.Ordinal);
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
