using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'index.ignore' and 'index.demote' (docs/csharp/sindri-profile.md): a demoted role stays indexed and reaches the engine as demote globs; a role in neither list is ignored.</summary>
public sealed class IndexDemoteTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _roles = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["test"] = ["**/*.test.ts"],
        ["generated"] = ["**/*.generated.ts"],
        ["demo"] = ["demo/**"],
        ["cron"] = ["**/*cron*"],
    };

    private static SindriProfile With(IndexSettings index) => new(1, "thor", Repos: [new RepoEntry("web-app", "/work/web-app", index)]);

    [Fact, Trait("Lane", "unit")]
    public void A_demoted_role_is_left_out_of_the_deny_globs_and_a_role_in_neither_list_is_kept()
    {
        var profile = With(new IndexSettings(Roles: _roles, Ignore: ["generated"], Demote: ["test", "demo"]));

        var deny = RepoDenyGlobs.For(profile, profile.Repos![0]);

        Assert.Equal(["**/*cron*", "**/*.generated.ts"], deny);   // why: roles come in name order
    }

    [Fact, Trait("Lane", "unit")]
    public void Roles_with_no_lists_are_all_ignored_as_before()
    {
        var profile = With(new IndexSettings(Roles: _roles));

        Assert.Equal(["**/*cron*", "demo/**", "**/*.generated.ts", "**/*.test.ts"], RepoDenyGlobs.For(profile, profile.Repos![0]));
        Assert.Null(RepoDemoteGlobs.For(profile, profile.Repos[0]));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_demote_globs_are_those_of_the_demoted_roles_in_name_order()
    {
        var profile = With(new IndexSettings(Roles: _roles, Demote: ["test", "demo"]));

        Assert.Equal(["demo/**", "**/*.test.ts"], RepoDemoteGlobs.For(profile, profile.Repos![0]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_that_demotes_nothing_has_no_demote_globs()
    {
        var profile = With(new IndexSettings(Roles: _roles, Ignore: ["test"]));

        Assert.Null(RepoDemoteGlobs.For(profile, profile.Repos![0]));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_engine_repo_carries_the_demote_globs_and_still_indexes_a_demoted_file()
    {
        var profile = With(new IndexSettings(Roles: _roles, Ignore: ["generated"], Demote: ["test"]));

        var repo = ScopeRules.Usable(ContextScopes.Entries(profile)).Single();

        Assert.True(repo.Demotes("src/a.test.ts"));
        Assert.False(repo.Denies("src/a.test.ts"));
        Assert.True(repo.Denies("src/a.generated.ts"));
        Assert.False(repo.Demotes("src/a.ts"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Usable_copies_the_entrys_demote_paths_into_the_context_repo()
    {
        var repo = ScopeRules.Usable([new ProfileRepoEntry("web-app", "/work/web-app", DemotePaths: ["demo/**"])]).Single();

        Assert.Equal(["demo/**"], repo.DemotePaths);
        Assert.Empty(ScopeRules.Usable([new ProfileRepoEntry("web-app", "/work/web-app")]).Single().DemotePaths);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_valid_ignore_and_demote_pass() =>
        Assert.True(ProfileRules.Check(With(new IndexSettings(Roles: _roles, Ignore: ["generated"], Demote: ["test", "demo"]))).IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_role_in_demote_names_its_key_path()
    {
        var profile = With(new IndexSettings(Roles: _roles, Demote: ["test", "nope"]));

        Assert.Contains("repos.web-app.index.demote[1]: 'nope' is not a role", ProfileRules.Check(profile).Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_role_in_ignore_names_its_key_path()
    {
        var profile = With(new IndexSettings(Roles: _roles, Ignore: ["nope"]));

        Assert.Contains("repos.web-app.index.ignore[0]: 'nope' is not a role", ProfileRules.Check(profile).Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_role_in_both_lists_is_refused_with_the_demote_key_path()
    {
        var profile = With(new IndexSettings(Roles: _roles, Ignore: ["test"], Demote: ["demo", "test"]));

        Assert.Contains("repos.web-app.index.demote[1]: 'test' is in both", ProfileRules.Check(profile).Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_role_name_is_refused(string name)
    {
        var profile = With(new IndexSettings(Roles: _roles, Demote: [name]));

        Assert.Contains("repos.web-app.index.demote[0]: must be a role name", ProfileRules.Check(profile).Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_level_ignore_or_demote_is_refused_because_they_are_per_repo()
    {
        Assert.Contains("index.ignore: ignore lists are per repo", ProfileRules.Check(new SindriProfile(1, "thor", Index: new IndexSettings(Ignore: ["x"]))).Error.Message, StringComparison.Ordinal);
        Assert.Contains("index.demote: demote lists are per repo", ProfileRules.Check(new SindriProfile(1, "thor", Index: new IndexSettings(Demote: ["x"]))).Error.Message, StringComparison.Ordinal);
    }
}
