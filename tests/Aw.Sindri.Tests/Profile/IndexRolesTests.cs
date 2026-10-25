using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'index.roles' (docs/csharp/sindri-profile.md): a map from a role name to globs; the files of a named role are never indexed, so they reach the engine as deny globs.</summary>
public sealed class IndexRolesTests : IDisposable
{
    private const string Base = "schemaVersion: 1\nuser: thor\n";
    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task The_yaml_profile_reads_index_roles_on_a_repo()
    {
        _files.Write(Base + "repos:\n  - { name: web, path: /work/web, index: { roles: { fixture: [\"**/fixtures/**\"] } } }\n");

        var profile = (await _files.Source.Load(Token)).Value.Profile;

        Assert.Equal(["**/fixtures/**"], profile.Repos![0].Index!.Roles!["fixture"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_roles_globs_join_the_deny_globs_of_each_repo_they_apply_to()
    {
        var profile = new SindriProfile(1, "thor",
            Repos: [new RepoEntry("web", "/work/web", new IndexSettings(["generated/**"], Roles: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["fixture"] = ["**/fixtures/**"] })), new RepoEntry("api", "/work/api")],
            Index: new IndexSettings(["vendor/**"]));

        var entries = ContextScopes.Entries(profile);

        Assert.Equal(["vendor/**", "generated/**", "**/fixtures/**"], entries.Single(e => e.Name == "web").DenyPaths);
        Assert.Equal(["vendor/**"], entries.Single(e => e.Name == "api").DenyPaths);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_of_a_named_role_is_denied_in_a_directory_and_by_name_and_other_files_are_not()
    {
        var profile = new SindriProfile(1, "thor",
            Repos: [new RepoEntry("web", "/work/web", new IndexSettings(Roles: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["cron"] = ["**/*cron*"], ["fixture"] = ["**/*fixture*"] }))]);

        var repo = ScopeRules.Usable(ContextScopes.Entries(profile)).Single();

        Assert.True(repo.Denies("src/crons/run.ts"));
        Assert.True(repo.Denies("tests/fixtures/data.ts"));
        Assert.True(repo.Denies("src/nightly-cron.ts"));
        Assert.False(repo.Denies("src/orders/place.ts"));
        Assert.True(repo.Denies(".env"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_with_no_roles_adds_no_deny_globs_and_nothing_is_excluded_by_default()
    {
        var entry = ContextScopes.Entries(new SindriProfile(1, "thor", Repos: [new RepoEntry("web", "/work/web")]))[0];

        Assert.Null(entry.DenyPaths);
        Assert.False(ScopeRules.Usable([entry]).Single().Denies("src/crons/run.ts"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("", "repos.web.index.roles.cron[0]: must be a non-empty glob")]
    [InlineData("/etc/**", "repos.web.index.roles.cron[0]: must be relative to the repo root")]
    [InlineData("a/../b", "repos.web.index.roles.cron[0]: must not contain '..'")]
    public void A_role_glob_that_cant_be_one_names_its_key_path(string glob, string message)
    {
        var profile = new SindriProfile(1, "thor", Repos: [new RepoEntry("web", "/work/web", new IndexSettings(Roles: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["cron"] = [glob] }))]);

        var error = ProfileRules.Check(profile).Error;

        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_role_glob_on_a_repo_names_the_repos_key_path()
    {
        var profile = new SindriProfile(1, "thor", Repos: [new RepoEntry("web", "/work/web", new IndexSettings(Roles: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["fixture"] = ["ok/**", "../x"] }))]);

        Assert.Contains("repos.web.index.roles.fixture[1]: must not contain '..'", ProfileRules.Check(profile).Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("prefix:test_")]
    [InlineData("suffix:.generated.ts")]
    [InlineData("regex:^v\\d+$")]
    public void A_prefix_suffix_or_regex_entry_is_a_valid_role_entry(string entry)
    {
        var profile = new SindriProfile(1, "thor", Repos: [new RepoEntry("web", "/work/web", new IndexSettings(Roles: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["generated"] = [entry] }))]);

        Assert.True(ProfileRules.Check(profile).IsSuccess);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("prefix:", "repos.web.index.roles.r[0]: 'prefix:' takes a text")]
    [InlineData("suffix:a/b", "repos.web.index.roles.r[0]: 'suffix:' matches one path segment")]
    [InlineData("regex:(", "repos.web.index.roles.r[0]: is not a valid regular expression")]
    public void A_bad_prefix_suffix_or_regex_entry_names_its_key_path(string entry, string message)
    {
        var profile = new SindriProfile(1, "thor", Repos: [new RepoEntry("web", "/work/web", new IndexSettings(Roles: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["r"] = [entry] }))]);

        Assert.Contains(message, ProfileRules.Check(profile).Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Tagged_entries_reach_the_engine_and_deny_by_segment()
    {
        var profile = new SindriProfile(1, "thor",
            Repos: [new RepoEntry("web", "/work/web", new IndexSettings(Roles: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["generated"] = ["suffix:.generated.ts", "prefix:gen_", "regex:^v\\d+$"] }))]);

        var repo = ScopeRules.Usable(ContextScopes.Entries(profile)).Single();

        Assert.True(repo.Denies("src/a.generated.ts"));
        Assert.True(repo.Denies("src/gen_x/a.ts"));
        Assert.True(repo.Denies("src/v3/a.ts"));
        Assert.False(repo.Denies("src/a.ts"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_level_index_roles_is_refused_because_roles_are_per_repo()
    {
        var profile = new SindriProfile(1, "thor", Index: new IndexSettings(Roles: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["cron"] = ["**/*cron*"] }));

        Assert.Contains("index.roles: roles are per repo", ProfileRules.Check(profile).Error.Message, StringComparison.Ordinal);
    }
}
