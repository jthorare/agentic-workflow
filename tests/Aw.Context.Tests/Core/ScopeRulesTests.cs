using Aw.Context.Core;
using Aw.Context.Core.Engine;

namespace Aw.Context.Tests.Core;

public sealed class ScopeRulesTests
{
    private static readonly ContextRepo[] _repos = [new(ContextRepoName.From("split-panes"), "/r/a"), new(ContextRepoName.From("other"), "/r/b")];

    [Fact, Trait("Lane", "unit")]
    public void A_human_reads_every_mapped_repo()
    {
        var scoped = ScopeRules.Resolve(new ContextSettings { Repos = [ContextRepoName.From("other")] }, _repos, new(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

        Assert.Equal(["other"], scoped.Value.Select(r => r.Name.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_session_is_refused_with_020_when_the_owner_turned_sessions_off()
    {
        var scoped = ScopeRules.Resolve(new ContextSettings { SessionsEnabled = false }, _repos, new(ContextCallerKind.Session, ContextRepoName.From("split-panes"), ContextClient.Http));

        Assert.Equal(ContextErrors.SessionsDisabled.Code, scoped.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("unknown-repo")]
    [InlineData("other")]
    public void A_session_outside_its_mapped_repo_gets_003_the_same_way(string? repo)
    {
        var settings = new ContextSettings { SessionsEnabled = true, Repos = [ContextRepoName.From("split-panes")] };

        var scoped = ScopeRules.Resolve(settings, _repos, new(ContextCallerKind.Session, repo is null ? null : ContextRepoName.From(repo), ContextClient.Mcp));

        Assert.Equal(ContextErrors.UnknownAnchor.Code, scoped.Error.Code);
        Assert.Equal("no context is mapped for this session's repo (X-Sindri-Repo)", scoped.Error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_session_that_names_no_repo_is_told_which_header_names_it()
    {
        var scoped = ScopeRules.Resolve(new ContextSettings(), _repos, new(ContextCallerKind.Session, Repo: null, ContextClient.Http));

        Assert.Equal(ContextErrors.McpRepoMissing.Code, scoped.Error.Code);
        Assert.Contains("X-Sindri-Repo: <repo> (e.g. X-Sindri-Repo: agentic-workflow)", scoped.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Sessions_are_on_by_default_and_a_session_sees_only_its_own_repo()
    {
        var scoped = ScopeRules.Resolve(new ContextSettings(), _repos, new(ContextCallerKind.Session, ContextRepoName.From("split-panes"), ContextClient.Http));

        Assert.Equal(["split-panes"], scoped.Value.Select(r => r.Name.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_context_repos_name_the_profile_lacks_is_001()
    {
        var scoped = ScopeRules.Resolve(new ContextSettings { Repos = [ContextRepoName.From("work_app")] }, _repos, new(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

        Assert.Equal(ContextErrors.SettingsInvalid.Code, scoped.Error.Code);
        Assert.StartsWith("context.repos: 'work_app' is not a profile repo", scoped.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_usable_repo_carries_the_profiles_deny_paths_on_top_of_the_built_in_secret_globs()
    {
        var usable = ScopeRules.Usable([new ProfileRepoEntry("split-panes", "/r/a", ["**/fixtures/**"]), new ProfileRepoEntry("other", "/r/b")]);

        var (withDeny, without) = (usable[0], usable[1]);
        Assert.True(withDeny.Denies("src/fixtures/big.ts"));
        Assert.True(withDeny.Denies(".env"));
        Assert.False(without.Denies("src/fixtures/big.ts"));
        Assert.True(without.Denies(".env"));
    }
}
