using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Core;

/// <summary>'context.sources' (spec §8.8): the onyx entry and the Linear teams its identifier aliases may name.</summary>
public sealed class SourcesSettingsTests
{
    private const string Onyx = """{"url":"http://127.0.0.1:3000","token":"env:ONYX_KEY","version":"v4.9.0"}""";

    private static string WithToken(string token) =>
        """{"sources":{"onyx":{"url":"http://127.0.0.1:3000","token":""" + JsonSerializer.Serialize(token) + ""","version":"v4.9.0"}}}""";

    private static Result<ContextSettings> Read(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void No_sources_means_no_onyx_and_no_teams()
    {
        var settings = Read("{}").Value;

        Assert.Null(settings.Onyx);
        Assert.Empty(settings.LinearTeams);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_onyx_entry_and_the_linear_teams_are_read_through_their_rules()
    {
        var settings = Read("""{"sources":{"onyx":""" + Onyx + ""","linear":{"teams":["SPL","APP"]}}}""").Value;

        Assert.NotNull(settings.Onyx);
        Assert.Equal(("127.0.0.1:3000", "env:ONYX_KEY", "v4.9.0"), (settings.Onyx.Address, settings.Onyx.Token, settings.Onyx.Version));
        Assert.Equal(["SPL", "APP"], settings.LinearTeams, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_linear_entry_with_a_token_gives_the_native_adapters_settings_and_teams_alone_give_none()
    {
        var native = Read("""{"sources":{"linear":{"token":"keychain:linear/work-read","teams":["SPL"]}}}""").Value;
        var teamsOnly = Read("""{"sources":{"linear":{"teams":["SPL"]}}}""").Value;

        Assert.Equal(("keychain:linear/work-read", "api.linear.app"), (native.Linear!.Token, native.Linear.Address));
        Assert.Equal(["SPL"], native.Linear.Teams, StringComparer.Ordinal);
        Assert.Equal(["SPL"], native.LinearTeams, StringComparer.Ordinal);
        Assert.Null(teamsOnly.Linear);
        Assert.Equal(["SPL"], teamsOnly.LinearTeams, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_managed_onyx_entry_as_sindri_onyx_up_writes_it_is_read_with_managed_set()
    {
        // The same four keys YamlOnyxProfileWriter writes: url, token, version, managed: true.
        var settings = Read("""{"sources":{"onyx":{"url":"http://127.0.0.1:3000","token":"keychain:onyx/sindri-onyx","version":"v4.9.0","managed":true}}}""").Value;

        Assert.True(settings.Onyx!.Managed);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_managed_onyx_entry_off_the_loopback_ip_is_055_at_the_reader()
    {
        var read = Read("""{"sources":{"onyx":{"url":"http://localhost:3000","token":"env:K","version":"v4.9.0","managed":true}}}""");

        Assert.Equal("SND-CONTEXT-055", read.Error.Code);
        Assert.StartsWith("context.sources.onyx", read.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"sources":{"slack":{}}}""", "SND-CONTEXT-001", "context.sources.slack:")]
    [InlineData("""{"sources":{"onyx":{"url":"http://127.0.0.1:3000","token":"env:K","version":"v4.9.0","apiKey":"x"}}}""", "SND-CONTEXT-001", "context.sources.onyx.apiKey:")]
    [InlineData("""{"sources":{"linear":{"teams":["SPL"],"apiKey":"x"}}}""", "SND-CONTEXT-001", "context.sources.linear.apiKey:")]
    [InlineData("""{"sources":{"linear":{"teams":["SPL"],"url":"https://evil.example/graphql","token":"env:K"}}}""", "SND-CONTEXT-070", "context.sources.linear.url:")]
    [InlineData("""{"sources":{"linear":{"teams":["SPL"],"url":"https://api.linear.app/graphql"}}}""", "SND-CONTEXT-002", "context.sources.linear.token:")]
    [InlineData("""{"sources":[]}""", "SND-CONTEXT-001", "context.sources:")]
    [InlineData("""{"sources":{"onyx":"http://127.0.0.1:3000"}}""", "SND-CONTEXT-001", "context.sources.onyx:")]
    [InlineData("""{"sources":{"linear":{"teams":["eng team"]}}}""", "SND-CONTEXT-001", "context.sources.linear.teams:")]
    [InlineData("""{"sources":{"linear":{"teams":["E"]}}}""", "SND-CONTEXT-001", "context.sources.linear.teams:")]   // one letter can't prefix an alias
    [InlineData("""{"sources":{"onyx":{"url":"http://10.0.0.5:3000","token":"env:K","version":"v4.9.0"}}}""", "SND-CONTEXT-055", "context.sources.onyx.url:")]
    [InlineData("""{"sources":{"onyx":{"url":"http://127.0.0.1:3000","token":"env:K","version":"latest"}}}""", "SND-CONTEXT-055", "context.sources.onyx.version:")]
    [InlineData("""{"sources":{"onyx":{"url":"http://127.0.0.1:3000","version":"v4.9.0"}}}""", "SND-CONTEXT-002", "context.sources.onyx.token:")]
    public void A_bad_sources_entry_is_refused_with_its_code_and_key_path(string json, string code, string keyPath)
    {
        var read = Read(json);

        Assert.Equal(code, read.Error.Code);
        Assert.StartsWith(keyPath, read.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("on_live-0123456789abcdef")]
    [InlineData("ONYX_KEY")]
    [InlineData("vault:onyx/sindri")]
    public void A_token_that_isnt_a_secret_pointer_is_refused_without_repeating_it(string token)
    {
        var read = Read(WithToken(token));

        Assert.Equal("SND-CONTEXT-002", read.Error.Code);
        Assert.StartsWith("context.sources.onyx.token:", read.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(token, read.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("env:ONYX_KEY")]
    [InlineData("file:/Users/me/.config/sindri/onyx.key")]
    [InlineData("keychain:onyx/sindri-onyx")]
    [InlineData("op:work/onyx/credential")]
    public void Every_pointer_scheme_is_accepted(string token) => Assert.Equal(token, Read(WithToken(token)).Value.Onyx?.Token);

    [Fact, Trait("Lane", "unit")]
    public void The_settings_error_names_its_catalogue_entry() => Assert.Equal(ContextErrors.SourceNotConfigured.Code, Read("""{"sources":{"onyx":{"url":"http://127.0.0.1:3000","version":"v4.9.0"}}}""").Error.Code);
}
