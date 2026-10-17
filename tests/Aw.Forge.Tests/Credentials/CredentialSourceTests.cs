using Aw.Forge.Core;
using Aw.Forge.Core.Credentials;
using Aw.Forge.Infrastructure.Credentials;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Forge.Tests.Credentials;

/// <summary>The two credential sources and the spec that picks one. No token value may appear in an error.</summary>
public sealed class CredentialSourceTests : IDisposable
{
    private const string Secret = "sekret-value-0003";
    private static int _scripts;
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("aw-forge-cred-");

    public void Dispose() => _dir.Delete(recursive: true);

    private string Script(string body)
    {
        var path = Path.Combine(_dir.FullName, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"cred-{Interlocked.Increment(ref _scripts)}.sh"));
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
        Executable.Make(path);
        return path;
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("env:AW_FORGE_TOKEN", ForgeCredentialKind.Env)]
    [InlineData("env:MY_TOKEN_2", ForgeCredentialKind.Env)]
    [InlineData("command:/usr/bin/true", ForgeCredentialKind.Command)]
    [InlineData("command:gh auth token", ForgeCredentialKind.Command)]
    public void A_credential_spec_parses(string value, ForgeCredentialKind kind) =>
        Assert.Equal(kind, ForgeCredentialSpec.Parse(value).Value.Kind);

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("literal-token-value")]
    [InlineData("env:")]
    [InlineData("env:lower")]
    [InlineData("command:")]
    [InlineData("command:   ")]
    public void A_bad_credential_spec_is_refused_without_echoing_the_value(string? value)
    {
        var parsed = ForgeCredentialSpec.Parse(value);

        Assert.Equal(ForgeErrors.Invalid.Code, parsed.Error.Code);
        if (value is { Length: > 8 })
        {
            Assert.DoesNotContain(value, parsed.Error.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_env_source_reads_and_trims_the_named_variable()
    {
        var source = new EnvCredentialSource("github.com", "AW_FORGE_TOKEN", name => name == "AW_FORGE_TOKEN" ? "  " + Secret + "\n" : null);

        var token = await source.Token("github.com", refresh: false, TestContext.Current.CancellationToken);

        Assert.Equal(Secret, token.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    public async Task An_unset_env_variable_is_AW_FORGE_003_naming_the_variable_only(string? value)
    {
        var source = new EnvCredentialSource("github.com", "AW_FORGE_TOKEN", _ => value);

        var token = await source.Token("github.com", refresh: false, TestContext.Current.CancellationToken);

        Assert.Equal(ForgeErrors.Unauthorized.Code, token.Error.Code);
        Assert.Contains("AW_FORGE_TOKEN", token.Error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("gitlab.example")]
    [InlineData("github.com.evil.example")]
    public async Task The_env_source_is_bound_to_its_host_and_gives_no_token_for_another(string other)
    {
        var source = new EnvCredentialSource("github.com", "AW_FORGE_TOKEN", _ => Secret);

        var bound = await source.Token("GitHub.com", refresh: false, TestContext.Current.CancellationToken);
        var refused = await source.Token(other, refresh: false, TestContext.Current.CancellationToken);

        Assert.Equal(Secret, bound.Value);
        Assert.Equal(ForgeErrors.Unauthorized.Code, refused.Error.Code);
        Assert.DoesNotContain(Secret, refused.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_command_source_is_bound_to_its_host_and_does_not_even_run_for_another()
    {
        var counter = Path.Combine(_dir.FullName, "calls");
        using var source = new CommandCredentialSource("github.com", [Script("echo x >> \"" + counter + "\"\necho " + Secret)], TimeProvider.System);
        var cancel = TestContext.Current.CancellationToken;

        var refused = await source.Token("gitlab.example", refresh: false, cancel);
        var bound = await source.Token("github.com", refresh: false, cancel);

        Assert.Equal(ForgeErrors.Unauthorized.Code, refused.Error.Code);
        Assert.Equal(Secret, bound.Value);
        _ = Assert.Single(await File.ReadAllLinesAsync(counter, cancel));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_command_prints_a_bare_token_that_is_cached_for_five_minutes_and_refetched_on_refresh()
    {
        var counter = Path.Combine(_dir.FullName, "calls");
        var script = Script("echo x >> \"" + counter + "\"\necho " + Secret);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-17T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        using var source = new CommandCredentialSource("github.com", [script], clock);
        var cancel = TestContext.Current.CancellationToken;

        var first = await source.Token("github.com", refresh: false, cancel);
        var second = await source.Token("github.com", refresh: false, cancel);
        clock.Advance(TimeSpan.FromMinutes(4));
        var third = await source.Token("github.com", refresh: false, cancel);
        _ = Assert.Single(await File.ReadAllLinesAsync(counter, cancel));
        clock.Advance(TimeSpan.FromMinutes(2));
        _ = await source.Token("github.com", refresh: false, cancel);
        _ = await source.Token("github.com", refresh: true, cancel);

        Assert.Equal([Secret, Secret, Secret], [first.Value, second.Value, third.Value]);
        Assert.Equal(3, (await File.ReadAllLinesAsync(counter, cancel)).Length);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_command_printing_json_is_cached_until_five_minutes_before_its_expiry()
    {
        var counter = Path.Combine(_dir.FullName, "calls");
        var script = Script("echo x >> \"" + counter + "\"\necho '{\"token\":\"" + Secret + "\",\"expiresAt\":\"2026-10-17T11:00:00Z\"}'");
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-17T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        using var source = new CommandCredentialSource("github.com", [script], clock);
        var cancel = TestContext.Current.CancellationToken;

        _ = await source.Token("github.com", refresh: false, cancel);
        clock.Advance(TimeSpan.FromMinutes(54));
        var cached = await source.Token("github.com", refresh: false, cancel);
        clock.Advance(TimeSpan.FromMinutes(2));
        _ = await source.Token("github.com", refresh: false, cancel);

        Assert.Equal(Secret, cached.Value);
        Assert.Equal(2, (await File.ReadAllLinesAsync(counter, cancel)).Length);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("exit 3")]
    [InlineData("true")]
    [InlineData("echo '{not json'")]
    [InlineData("echo '{\"other\":1}'")]
    public async Task A_failing_or_empty_command_is_AW_FORGE_003_and_never_carries_the_output(string body)
    {
        using var source = new CommandCredentialSource("github.com", [Script("echo " + Secret + " >&2\n" + body)], TimeProvider.System);

        var token = await source.Token("github.com", refresh: false, TestContext.Current.CancellationToken);

        Assert.Equal(ForgeErrors.Unauthorized.Code, token.Error.Code);
        Assert.DoesNotContain(Secret, token.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_command_that_does_not_exist_is_AW_FORGE_003()
    {
        using var source = new CommandCredentialSource("github.com", [Path.Combine(_dir.FullName, "absent")], TimeProvider.System);

        var token = await source.Token("github.com", refresh: false, TestContext.Current.CancellationToken);

        Assert.Equal(ForgeErrors.Unauthorized.Code, token.Error.Code);
    }
}
