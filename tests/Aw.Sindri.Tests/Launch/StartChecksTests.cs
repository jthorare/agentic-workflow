using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Launch;

/// <summary>
/// The sign-in check before a one-off start (agents spec §15.4, #3471): Claude Code keeps its login in $CLAUDE_CONFIG_DIR when that is set, so the check reads there before the default home.
/// </summary>
public sealed class StartChecksTests : IDisposable
{
    private static readonly AgentEntry _claude = new(AgentId.From("claude-code"), "Claude Code", "claude", ProcessName: null, Transcripts: null, Resume: null);

    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_login_file_in_CLAUDE_CONFIG_DIR_counts_as_signed_in()
    {
        var config = Directory.CreateDirectory(Path.Combine(_home.Path, "claude-config")).FullName;
        await File.WriteAllTextAsync(Path.Combine(config, ".credentials.json"), "{}", Token);

        var ready = await Checks(("CLAUDE_CONFIG_DIR", config)).Agent(_claude, Token);

        Assert.True(ready.SignedIn);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_account_in_CLAUDE_CONFIG_DIR_claude_json_counts_as_signed_in()
    {
        var config = Directory.CreateDirectory(Path.Combine(_home.Path, "claude-config")).FullName;
        await File.WriteAllTextAsync(Path.Combine(config, ".claude.json"), "{\"oauthAccount\":{}}", Token);

        var ready = await Checks(("CLAUDE_CONFIG_DIR", config)).Agent(_claude, Token);

        Assert.True(ready.SignedIn);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_login_anywhere_the_agent_is_not_signed_in()
    {
        var config = Directory.CreateDirectory(Path.Combine(_home.Path, "claude-config")).FullName;

        var ready = await Checks(("CLAUDE_CONFIG_DIR", config)).Agent(_claude, Token);

        Assert.False(ready.SignedIn);
    }

    private StartChecks Checks(params (string Name, string Value)[] set)
    {
        var environment = set.ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
        environment["HOME"] = Directory.CreateDirectory(Path.Combine(_home.Path, "home")).FullName;
        return new StartChecks(name => environment.GetValueOrDefault(name));
    }
}
