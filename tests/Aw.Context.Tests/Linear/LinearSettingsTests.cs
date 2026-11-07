using Aw.Context.Core.Linear;

namespace Aw.Context.Tests.Linear;

/// <summary>The 'context.sources.linear' rules (spec §8.8, §14 S-L2): Linear's own https host, or a loopback fixture with a token that stays local; a token pointer.</summary>
public sealed class LinearSettingsTests
{
    private const string Pointer = "keychain:linear/work-read";

    [Fact, Trait("Lane", "unit")]
    public void No_url_means_linears_own_api()
    {
        var settings = LinearSettings.Create(url: null, Pointer, teams: ["ENG"]).Value;

        Assert.Equal(new Uri("https://api.linear.app/graphql"), settings.Url);
        Assert.Equal((Pointer, "api.linear.app"), (settings.Token, settings.Address));
        Assert.Equal(["ENG"], settings.Teams, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("https://api.linear.app", "https://api.linear.app/graphql")]
    [InlineData("https://api.linear.app/", "https://api.linear.app/graphql")]
    [InlineData("https://API.linear.app/graphql", "https://api.linear.app/graphql")]
    public void Linears_host_over_https_is_accepted_and_ends_at_the_graphql_route(string url, string expected)
        => Assert.Equal(new Uri(expected), LinearSettings.Create(url, Pointer, teams: []).Value.Url);

    [Theory, Trait("Lane", "unit")]
    [InlineData("http://127.0.0.1:4010/graphql", "env:LINEAR_FIXTURE_KEY")]
    [InlineData("http://localhost:4010", "file:/run/secrets/linear")]
    [InlineData("https://[::1]:4010/graphql", "env:LINEAR_FIXTURE_KEY")]
    public void A_loopback_url_is_accepted_only_for_an_env_or_file_token(string url, string token)
    {
        var settings = LinearSettings.Create(url, token, teams: ["SPL"]);

        Assert.True(settings.IsSuccess, settings.IsError ? settings.Error.Message : "");
        Assert.EndsWith("/graphql", settings.Value.Url.AbsolutePath, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("keychain:linear/work-read")]
    [InlineData("op:vault/linear/key")]
    public void A_loopback_url_with_a_keychain_or_op_token_is_070_so_the_real_key_never_reaches_a_local_listener(string token)
    {
        var error = LinearSettings.Create("http://127.0.0.1:4010/graphql", token, teams: []).Error;

        Assert.Equal("SND-CONTEXT-070", error.Code);
        Assert.StartsWith("context.sources.linear.url:", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("http://api.linear.app/graphql")]
    [InlineData("https://evil.example/graphql")]
    [InlineData("https://api.linear.app.evil.example/graphql")]
    [InlineData("https://api.linear.app:8443/graphql")]
    [InlineData("https://user:pw@api.linear.app/graphql")]
    [InlineData("https://api.linear.app/graphql?x=1")]
    [InlineData("ftp://api.linear.app/graphql")]
    [InlineData("not a url")]
    public void Any_other_url_is_070_naming_the_key_path(string url)
    {
        var error = LinearSettings.Create(url, "env:K", teams: []).Error;

        Assert.Equal("SND-CONTEXT-070", error.Code);
        Assert.StartsWith("context.sources.linear.url:", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("pw", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    public void A_missing_token_is_002(string? token)
    {
        var error = LinearSettings.Create(url: null, token, teams: []).Error;

        Assert.Equal("SND-CONTEXT-002", error.Code);
        Assert.StartsWith("context.sources.linear.token:", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_token_that_is_not_a_secret_pointer_is_002_and_is_not_repeated()
    {
        var error = LinearSettings.Create(url: null, "lin_api_0123456789abcdef", teams: []).Error;

        Assert.Equal("SND-CONTEXT-002", error.Code);
        Assert.DoesNotContain("lin_api", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void ToString_withholds_the_token()
        => Assert.DoesNotContain("work-read", LinearSettings.Create(url: null, Pointer, teams: []).Value.ToString(), StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void No_write_token_means_none()
        => Assert.Null(LinearSettings.Create(url: null, Pointer, teams: []).Value.WriteToken);

    [Fact, Trait("Lane", "unit")]
    public void A_write_token_pointer_is_kept()
        => Assert.Equal("op:vault/linear/write", LinearSettings.Create(url: null, Pointer, teams: [], writeToken: "op:vault/linear/write").Value.WriteToken);

    [Fact, Trait("Lane", "unit")]
    public void A_write_token_that_is_not_a_secret_pointer_is_002_naming_its_key_and_is_not_repeated()
    {
        var error = LinearSettings.Create(url: null, Pointer, teams: [], writeToken: "a-key-written-into-the-profile").Error;

        Assert.Equal("SND-CONTEXT-002", error.Code);
        Assert.StartsWith("context.sources.linear.writeToken:", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("a-key-written", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("keychain:linear/work-write")]
    [InlineData("op:vault/linear/write")]
    public void A_loopback_url_with_a_keychain_or_op_write_token_is_070_so_the_real_write_key_never_reaches_a_local_listener(string writeToken)
    {
        var error = LinearSettings.Create("http://127.0.0.1:4010/graphql", "env:LINEAR_FIXTURE_KEY", teams: [], writeToken).Error;

        Assert.Equal("SND-CONTEXT-070", error.Code);
        Assert.StartsWith("context.sources.linear.url:", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(writeToken, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_loopback_url_takes_an_env_write_token()
        => Assert.True(LinearSettings.Create("http://127.0.0.1:4010/graphql", "env:LINEAR_FIXTURE_KEY", teams: [], writeToken: "env:LINEAR_FIXTURE_WRITE_KEY").IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public void Linears_own_host_takes_any_write_token_scheme_because_the_profile_check_owns_that_rule()
        => Assert.True(LinearSettings.Create(url: null, Pointer, teams: [], writeToken: "env:LINEAR_WRITE_KEY").IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public void ToString_withholds_the_write_token()
        => Assert.DoesNotContain("work-write", LinearSettings.Create(url: null, Pointer, teams: [], writeToken: "keychain:linear/work-write").Value.ToString(), StringComparison.Ordinal);
}
