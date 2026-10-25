using Aw.Context.Core;
using Aw.Context.Core.Onyx;
using Aw.Context.Testing.Fixtures;

namespace Aw.Context.Tests.Onyx;

/// <summary>The 'context.sources.onyx' rules (spec §8.8, §14 S-L2): loopback only, a pinned release tag, a token pointer.</summary>
public sealed class OnyxSettingsTests
{
    private const string Pointer = "env:ONYX_FIXTURE_KEY";

    [Theory, Trait("Lane", "unit")]
    [InlineData("http://127.0.0.1:3189", "http://127.0.0.1:3189/")]
    [InlineData("http://127.0.0.1:3189/", "http://127.0.0.1:3189/")]
    [InlineData("http://127.4.5.6:8080", "http://127.4.5.6:8080/")]
    [InlineData("https://[::1]:3443", "https://[::1]:3443/")]
    [InlineData("http://localhost:3189", "http://localhost:3189/")]
    [InlineData("http://LOCALHOST:3189", "http://localhost:3189/")]
    [InlineData("http://127.0.0.1:3189/onyx", "http://127.0.0.1:3189/onyx/")]
    public void A_loopback_url_is_accepted_and_ends_in_a_slash(string url, string expected)
    {
        var settings = OnyxSettings.Create(url, Pointer, "v4.9.0");

        Assert.True(settings.IsSuccess, settings.IsError ? settings.Error.Message : "");
        Assert.Equal(new Uri(expected), settings.Value.Url);
        Assert.Equal(Pointer, settings.Value.Token);
        Assert.Equal("v4.9.0", settings.Value.Version);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_managed_onyx_is_reached_at_127_0_0_1_over_http_and_unmanaged_is_the_default()
    {
        Assert.True(OnyxSettings.Create("http://127.0.0.1:3190", Pointer, "v4.9.0", managed: true).Value.Managed);
        Assert.False(OnyxSettings.Create("http://127.0.0.1:3190", Pointer, "v4.9.0").Value.Managed);
        Assert.False(OnyxSettings.Create("http://localhost:3189", Pointer, "v4.9.0").Value.Managed);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("http://localhost:3189")]
    [InlineData("http://[::1]:3189")]
    [InlineData("https://127.0.0.1:3189")]
    public void A_managed_onyx_on_any_other_loopback_form_is_055(string url)
    {
        var error = OnyxSettings.Create(url, Pointer, "v4.9.0", managed: true).Error;

        Assert.Equal("SND-CONTEXT-055", error.Code);
        Assert.Contains("managed", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("http://10.0.0.5:3189", "10.0.0.5")]
    [InlineData("http://0.0.0.0:3189", "0.0.0.0")]
    [InlineData("http://192.168.1.20:3189", "192.168.1.20")]
    [InlineData("https://onyx.fixture-labs.invalid", "onyx.fixture-labs.invalid")]
    [InlineData("http://[::]:3189", "[::]")]
    [InlineData("http://[::ffff:127.0.0.1]:3189", "[::ffff:127.0.0.1]")]
    [InlineData("http://localhost.fixture-labs.invalid:3189", "localhost.fixture-labs.invalid")]
    public void A_url_off_loopback_is_055_naming_the_key_path_the_host_and_the_fix(string url, string host)
    {
        var error = OnyxSettings.Create(url, Pointer, "v4.9.0").Error;

        Assert.Equal(ContextErrors.OnyxNotPinnedOrLoopback.Code, error.Code);
        Assert.StartsWith("context.sources.onyx.url: ", error.Message, StringComparison.Ordinal);
        Assert.Contains(host, error.Message, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("127.0.0.1:3189")]
    [InlineData("/tmp/onyx.sock")]
    [InlineData("ftp://127.0.0.1/")]
    [InlineData("file:///tmp/onyx")]
    [InlineData("not a url")]
    public void A_url_that_is_not_absolute_http_or_https_is_055(string? url)
    {
        var error = OnyxSettings.Create(url, Pointer, "v4.9.0").Error;

        Assert.Equal(ContextErrors.OnyxNotPinnedOrLoopback.Code, error.Code);
        Assert.StartsWith("context.sources.onyx.url: ", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_url_with_a_user_or_password_is_055_and_the_message_never_repeats_them()
    {
        var error = OnyxSettings.Create("http://admin:" + "planted-secret-7f3a@127.0.0.1:3189", Pointer, "v4.9.0").Error;

        Assert.Equal(ContextErrors.OnyxNotPinnedOrLoopback.Code, error.Code);
        Assert.StartsWith("context.sources.onyx.url: ", error.Message, StringComparison.Ordinal);
        Assert.Contains("context.sources.onyx.token", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("planted-secret-7f3a", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("admin", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("v4.9.0")]
    [InlineData("4.9.0")]
    [InlineData("v10.0.12")]
    [InlineData("v4.9.0-beta.1")]
    public void A_release_tag_is_a_pinned_version(string version) =>
        Assert.True(OnyxSettings.Create("http://127.0.0.1:3189", Pointer, version).IsSuccess);

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, "required")]
    [InlineData("", "required")]
    [InlineData("  ", "required")]
    [InlineData("latest", "latest")]
    [InlineData("LATEST", "latest")]
    [InlineData("edge", "release tag")]
    [InlineData("4.9", "release tag")]
    [InlineData("v4.9.0 ", "release tag")]
    [InlineData("main", "release tag")]
    public void A_version_that_is_not_a_release_tag_is_055_naming_the_key_path(string? version, string says)
    {
        var error = OnyxSettings.Create("http://127.0.0.1:3189", Pointer, version).Error;

        Assert.Equal(ContextErrors.OnyxNotPinnedOrLoopback.Code, error.Code);
        Assert.StartsWith("context.sources.onyx.version: ", error.Message, StringComparison.Ordinal);
        Assert.Contains(says, error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    public void No_token_pointer_is_002_naming_the_key_path(string? token)
    {
        var error = OnyxSettings.Create("http://127.0.0.1:3189", token, "v4.9.0").Error;

        Assert.Equal(ContextErrors.SourceNotConfigured.Code, error.Code);
        Assert.StartsWith("context.sources.onyx.token: ", error.Message, StringComparison.Ordinal);
    }

    public static TheoryData<string> SindriPointers => [.. SecretPointerSamples.Accepted];

    // The same list Aw.Sindri.Tests' SecretPointerAgreementTests runs through Sindri's SecretPointer.Parse: every
    // pointer Sindri accepts passes this check, which asks only for a known scheme and a non-empty target.
    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(SindriPointers))]
    public void Every_pointer_sindri_accepts_is_accepted(string token)
    {
        var settings = OnyxSettings.Create("http://127.0.0.1:3189", token, "v4.9.0");

        Assert.True(settings.IsSuccess, settings.IsError ? settings.Error.Message : null);
        Assert.Equal(token, settings.Value.Token);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("env:")]
    [InlineData("keychain:")]
    [InlineData("vault:onyx/sindri")]
    [InlineData("on_live-0123456789abcdef")]
    public void A_token_without_a_known_scheme_and_a_target_is_002(string token)
        => Assert.Equal(ContextErrors.SourceNotConfigured.Code, OnyxSettings.Create("http://127.0.0.1:3189", token, "v4.9.0").Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void No_error_or_description_repeats_the_token()
    {
        const string planted = "keychain:onyx/planted-key-55d1";
        const string written = "on_planted-key-55d1";   // a key written into the profile instead of a pointer

        var plainKey = OnyxSettings.Create("http://127.0.0.1:3189", written, "v4.9.0").Error.Message;
        var badUrl = OnyxSettings.Create("http://10.0.0.5:3189", planted, "v4.9.0").Error.Message;
        var badVersion = OnyxSettings.Create("http://127.0.0.1:3189", planted, "latest").Error.Message;
        var settings = OnyxSettings.Create("http://127.0.0.1:3189", planted, "v4.9.0").Value;
        var drift = settings.CheckVersion("v4.8.1").Error.Message;

        Assert.All([badUrl, badVersion, drift, settings.ToString()], text => Assert.DoesNotContain(planted, text, StringComparison.Ordinal));
        Assert.DoesNotContain(written, plainKey, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("v4.9.0", "v4.9.0")]
    [InlineData("v4.9.0", "4.9.0")]
    [InlineData("4.9.0", "v4.9.0")]
    [InlineData("4.9.0", "4.9.0")]
    public void The_reported_version_matches_the_pin_with_or_without_a_leading_v(string pinned, string reported)
    {
        var settings = OnyxSettings.Create("http://127.0.0.1:3189", Pointer, pinned).Value;

        Assert.True(settings.CheckVersion(reported).IsSuccess);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("v4.8.1")]
    [InlineData("v4.9.1")]
    [InlineData("v4.9.0-beta.1")]
    [InlineData("Development")]
    [InlineData("0.0.0-dev")]
    [InlineData("")]
    [InlineData("V4.9.0")]
    public void Another_reported_version_is_055_naming_both_versions_and_the_fix(string reported)
    {
        var settings = OnyxSettings.Create("http://127.0.0.1:3189", Pointer, "v4.9.0").Value;

        var error = settings.CheckVersion(reported).Error;

        Assert.Equal(ContextErrors.OnyxNotPinnedOrLoopback.Code, error.Code);
        Assert.StartsWith("context.sources.onyx.version: ", error.Message, StringComparison.Ordinal);
        Assert.Contains("'v4.9.0'", error.Message, StringComparison.Ordinal);
        Assert.Contains($"'{reported}'", error.Message, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:3189", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_long_or_multi_line_reported_version_is_cut_and_flattened_in_the_message()
    {
        var settings = OnyxSettings.Create("http://127.0.0.1:3189", Pointer, "v4.9.0").Value;

        var error = settings.CheckVersion("v4.9.0\nextra " + new string('x', 500)).Error;

        Assert.DoesNotContain("\n", error.Message, StringComparison.Ordinal);
        Assert.True(error.Message.Length < 400, error.Message);
    }
}
