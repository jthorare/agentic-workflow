using Aw.Context.Core.Engine;

namespace Aw.Context.Tests.Core;

public sealed class PathDenylistTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(".env")]
    [InlineData(".env.local")]
    [InlineData("config/.env.production")]
    [InlineData("certs/server.pem")]
    [InlineData("deploy/id_rsa")]
    [InlineData("infra/prod.tfstate")]
    [InlineData("a/b/credentials.json")]
    [InlineData("app/secrets/token.txt")]
    [InlineData("secrets/token.txt")]
    [InlineData(".npmrc")]
    public void The_built_in_secret_globs_deny_what_they_name(string path) =>
        Assert.True(PathDenylist.Matches(path, PathDenylist.Defaults));

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/app.ts")]
    [InlineData("README.md")]
    [InlineData("src/environment.ts")]
    [InlineData("docs/secrets-policy.md")]
    public void Ordinary_files_are_not_denied(string path) =>
        Assert.False(PathDenylist.Matches(path, PathDenylist.Defaults));

    [Theory, Trait("Lane", "unit")]
    [InlineData("**/fixtures/**", "src/Fixtures/a.ts", true)]
    [InlineData("**/fixtures/**", "fixtures/a/b.ts", true)]
    [InlineData("**/fixtures/**", "src/fixture/a.ts", false)]
    [InlineData("legacy/**", "legacy/old/a.ts", true)]
    [InlineData("legacy/**", "src/legacy/a.ts", false)]
    [InlineData("*.gen.ts", "a.gen.ts", true)]
    [InlineData("*.gen.ts", "src/a.gen.ts", false)]
    [InlineData("**/*.gen.ts", "src/deep/a.gen.ts", true)]
    [InlineData("src/?.ts", "src/a.ts", true)]
    [InlineData("src/?.ts", "src/ab.ts", false)]
    [InlineData("src/a+b(1).ts", "src/a+b(1).ts", true)]
    public void A_custom_glob_matches_without_regard_to_case_and_keeps_to_its_segments(string glob, string path, bool denied) =>
        Assert.Equal(denied, PathDenylist.Matches(path, [glob]));

    [Theory, Trait("Lane", "unit")]
    [InlineData("**/*cron*", "src/crons/run.ts", true)]
    [InlineData("**/*cron*", "src/jobs/nightly-cron.ts", true)]
    [InlineData("**/*fixture*", "tests/fixtures/data.ts", true)]
    [InlineData("**/*fixture*", "tests/Fixtures/deep/er/data.ts", true)]
    [InlineData("**/*router.*", "src/routers/index.ts", false)]
    [InlineData("**/*router*", "src/routers/index.ts", true)]
    [InlineData("**/*router.*", "src/app.router.ts", true)]
    [InlineData("legacy", "legacy/old/a.ts", true)]
    [InlineData("src/*", "src/a/b.ts", true)]
    [InlineData("src/a", "src/ab/b.ts", false)]
    [InlineData("**/*cron*", "src/scheduler/run.ts", false)]
    public void A_glob_that_matches_a_directory_denies_everything_under_it(string glob, string path, bool denied)
    {
        Assert.Equal(denied, PathDenylist.Matches(path, [glob]));
        Assert.Equal(denied && !path.Any(char.IsUpper), PathDenylist.MatchesExactCase(path, [glob]));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("prefix:test_", "src/test_helpers.py", true)]
    [InlineData("prefix:test_", "src/TEST_dir/a.py", true)]
    [InlineData("prefix:test_", "src/my_test_a.py", false)]
    [InlineData("suffix:.generated.ts", "src/a.generated.ts", true)]
    [InlineData("suffix:.generated.ts", "src/a.generated.tsx", false)]
    [InlineData("suffix:_old", "src/a_old/b.ts", true)]
    [InlineData("regex:^v\\d+$", "src/v2/a.ts", true)]
    [InlineData("regex:^v\\d+$", "src/v2b/a.ts", false)]
    [InlineData("regex:\\.(snap|golden)$", "tests/a.golden", true)]
    [InlineData("regex:\\.(snap|golden)$", "tests/a.txt", false)]
    public void A_tagged_role_entry_matches_a_path_segment(string entry, string path, bool denied) =>
        Assert.Equal(denied, PathDenylist.Matches(path, [entry]));

    [Fact, Trait("Lane", "unit")]
    public void A_tagged_entry_compares_case_exactly_in_the_exact_case_matcher() =>
        Assert.False(PathDenylist.MatchesExactCase("src/TEST_a.py", ["prefix:test_"]));

    [Fact, Trait("Lane", "unit")]
    public void A_repo_denies_through_its_own_list_and_the_default_list_is_the_built_ins()
    {
        var plain = new ContextRepo(ContextRepoName.From("app"), "/work/app");
        var custom = plain with { DenyPaths = [.. PathDenylist.Defaults, "private/**"] };

        Assert.True(plain.Denies(".env"));
        Assert.False(plain.Denies("private/a.ts"));
        Assert.True(custom.Denies("private/a.ts"));
        Assert.True(custom.Denies("config/secrets/x.json"));
    }
}
