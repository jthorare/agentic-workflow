using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's top-level 'review:' block (PR review page spec §7, §9): SND-REVIEW-001, 003 and 005, and the pure SND-REVIEW-002 check.</summary>
public sealed class PrReviewRulesTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    private static readonly string[] _codes = ["SND-REVIEW-001", "SND-REVIEW-002", "SND-REVIEW-003", "SND-REVIEW-005", "SND-REVIEW-503"];

    private static SindriProfile With(PrReviewSettings review) => _valid with { Review = review };

    private static PrReviewSettings Entry(PrReviewSettingsEntry entry) => new(Settings: new Dictionary<string, PrReviewSettingsEntry>(StringComparer.Ordinal) { ["sindri"] = entry });

    private static PrReviewSettings Check(PrReviewCheck check) => Entry(new(Checks: [check]));

    [Fact, Trait("Lane", "unit")]
    public void A_profile_without_the_block_is_accepted() => Assert.True(ProfileRules.Check(_valid).IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public void A_full_valid_block_is_accepted()
    {
        var review = new PrReviewSettings(
            "joi-example",
            "command:gh auth token",
            new Dictionary<string, PrReviewSettingsEntry>(StringComparer.Ordinal)
            {
                ["sindri"] = new(
                    Extends: "default",
                    Panels: ["requiredChecks", "ci"],
                    Groups: [new("Core", ["src/*/*.Core/**"])],
                    Collapse: ["**/*.g.cs"],
                    Risky: [new([".github/workflows/**"], "CI definition")],
                    Checks:
                    [
                        new("ui", "Ui tests ran", ["recipes/**/*.yaml"], new(Ci: "ui-tests", App: "github-actions")),
                        new("locks", "Locks with csproj", ["**/*.csproj"], new(ChangedWith: ["**/packages.lock.json"])),
                        new("covers", "Names its doc", Need: new(Covers: true)),
                        new("ideas", "Ideas reviewed it", Need: new(Body: "^reviewed by ideas at [0-9a-f]{7,40}$")),
                        new("mine", "I ran it", Need: new(Manual: true)),
                    ]),
            },
            [new("prj_1", ["jthorare/agentic-workflow"], "sindri")]);
        var context = System.Text.Json.JsonDocument.Parse("""{"sources":{"github":{"repos":["jthorare/agentic-workflow"]}}}""").RootElement;

        Assert.True(ProfileRules.Check(With(review) with { Context = context }).IsSuccess);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("a")]
    [InlineData("joi-example")]
    [InlineData("A1-b2")]
    public void A_github_login_is_accepted(string login) => Assert.True(PrReviewRules.IsLogin(login));

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("-bad-")]
    [InlineData("bad-")]
    [InlineData("a--b")]
    [InlineData("has space")]
    [InlineData("under_score")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmn")]
    public void A_bad_login_is_SND_REVIEW_001_naming_review_login(string login)
    {
        var error = ProfileRules.Check(With(new PrReviewSettings(Login: login))).Error;

        Assert.Equal("SND-REVIEW-001", error.Code);
        Assert.StartsWith("review.login:", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("ghp_literal_token", "review.credential")]
    [InlineData("env:lower", "review.credential")]
    [InlineData("token:s3cr3t", "review.credential")]
    public void A_credential_that_is_not_a_pointer_is_refused_and_never_echoed(string credential, string key)
    {
        var error = ProfileRules.Check(With(new PrReviewSettings(Credential: credential))).Error;

        Assert.Equal("SND-REVIEW-001", error.Code);
        Assert.Contains(key, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(credential, error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("env:AW_REVIEW_GITHUB_TOKEN")]
    [InlineData("env:MY_TOKEN")]
    [InlineData("command:gh auth token")]
    public void A_credential_pointer_follows_the_forge_credential_grammar(string credential) =>
        Assert.True(ProfileRules.Check(With(new PrReviewSettings(Credential: credential))).IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public void The_credential_variable_is_the_env_name_the_default_or_none_for_a_command()
    {
        Assert.Equal("AW_REVIEW_GITHUB_TOKEN", PrReviewRules.CredentialVariable(review: null));
        Assert.Equal("AW_REVIEW_GITHUB_TOKEN", PrReviewRules.CredentialVariable(new PrReviewSettings()));
        Assert.Equal("MY_TOKEN", PrReviewRules.CredentialVariable(new PrReviewSettings(Credential: "env:MY_TOKEN")));
        Assert.Null(PrReviewRules.CredentialVariable(new PrReviewSettings(Credential: "command:gh auth token")));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_panel_is_refused_listing_the_allowed_ones()
    {
        var error = ProfileRules.Check(With(Entry(new(Panels: ["requiredChecks", "nope"])))).Error;

        Assert.Equal("SND-REVIEW-001", error.Code);
        Assert.Contains("review.settings.sindri.panels[1]", error.Message, StringComparison.Ordinal);
        Assert.Contains("conversation", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_panel_listed_twice_is_refused()
    {
        var error = ProfileRules.Check(With(Entry(new(Panels: ["ci", "ci"])))).Error;

        Assert.Equal("SND-REVIEW-001", error.Code);
        Assert.Contains("panels[1]", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/abs/**")]
    [InlineData("a/../b")]
    [InlineData("a\\b")]
    [InlineData("")]
    public void A_bad_glob_is_refused_in_collapse_groups_risky_and_when(string glob)
    {
        Assert.Equal("SND-REVIEW-001", ProfileRules.Check(With(Entry(new(Collapse: [glob])))).Error.Code);
        Assert.Equal("SND-REVIEW-001", ProfileRules.Check(With(Entry(new(Groups: [new("G", [glob])])))).Error.Code);
        Assert.Equal("SND-REVIEW-001", ProfileRules.Check(With(Entry(new(Risky: [new([glob], "why")])))).Error.Code);
        Assert.Equal("SND-REVIEW-001", ProfileRules.Check(With(Check(new("c", "t", [glob], new(Manual: true))))).Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("(unclosed")]
    [InlineData("")]
    public void A_body_regex_that_does_not_compile_is_refused(string pattern) =>
        Assert.Equal("SND-REVIEW-001", ProfileRules.Check(With(Check(new("c", "t", Need: new(Body: pattern))))).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void A_body_regex_over_512_characters_is_refused() =>
        Assert.Contains("512", ProfileRules.Check(With(Check(new("c", "t", Need: new(Body: new string('a', 513)))))).Error.Message, StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_duplicate_check_id_is_refused()
    {
        var error = ProfileRules.Check(With(Entry(new(Checks: [new("c", "one", Need: new(Manual: true)), new("c", "two", Need: new(Manual: true))])))).Error;

        Assert.Equal("SND-REVIEW-001", error.Code);
        Assert.Contains("checks[1].id", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_when_list_is_refused_but_a_missing_one_is_fine()
    {
        Assert.Contains("when", ProfileRules.Check(With(Check(new("c", "t", [], new(Manual: true))))).Error.Message, StringComparison.Ordinal);
        Assert.True(ProfileRules.Check(With(Check(new("c", "t", When: null, new(Manual: true))))).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_need_with_no_key_or_two_keys_is_refused()
    {
        Assert.Equal("SND-REVIEW-001", ProfileRules.Check(With(Check(new("c", "t", Need: new())))).Error.Code);
        Assert.Equal("SND-REVIEW-001", ProfileRules.Check(With(Check(new("c", "t", Need: new(Ci: "x", Manual: true))))).Error.Code);
        Assert.Equal("SND-REVIEW-001", ProfileRules.Check(With(Check(new("c", "t", Need: null)))).Error.Code);
        Assert.Equal("SND-REVIEW-001", ProfileRules.Check(With(Check(new("c", "t", Need: new(App: "github-actions"))))).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_command_need_is_SND_REVIEW_003_with_the_ci_hint()
    {
        var error = ProfileRules.Check(With(Check(new("c", "t", Need: new(Command: "dotnet test"))))).Error;

        Assert.Equal("SND-REVIEW-003", error.Code);
        Assert.Contains("Command checks wait for the sandbox. Use `ci:` with the CI job that runs it.", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_extends_or_projects_settings_is_refused()
    {
        Assert.Contains("extends", ProfileRules.Check(With(Entry(new(Extends: "ghost")))).Error.Message, StringComparison.Ordinal);
        var review = new PrReviewSettings(Projects: [new("prj_1", Settings: "ghost")]);
        Assert.Contains("review.projects[0].settings", ProfileRules.Check(With(review)).Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_extends_cycle_is_refused()
    {
        var review = new PrReviewSettings(Settings: new Dictionary<string, PrReviewSettingsEntry>(StringComparer.Ordinal)
        {
            ["a"] = new(Extends: "b"),
            ["b"] = new(Extends: "a"),
        });

        var error = ProfileRules.Check(With(review)).Error;

        Assert.Equal("SND-REVIEW-001", error.Code);
        Assert.Contains("loop", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_that_is_not_synced_is_SND_REVIEW_005_naming_the_key_path()
    {
        var review = new PrReviewSettings(Projects: [new("prj_1", ["jthorare/agentic-workflow"])]);

        var error = ProfileRules.Check(With(review)).Error;

        Assert.Equal("SND-REVIEW-005", error.Code);
        Assert.Contains("review.projects[0].repos[0]", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_project_that_is_not_on_the_page_is_SND_REVIEW_002()
    {
        var review = new PrReviewSettings(Projects: [new("prj_1"), new("prj_2")]);

        var error = PrReviewRules.UnknownProject(review, new HashSet<string>(["prj_1"], StringComparer.Ordinal));

        Assert.Equal("SND-REVIEW-002", error!.Code);
        Assert.Contains("No project prj_2. `sindri projects` lists them.", error.Message, StringComparison.Ordinal);
        Assert.Null(PrReviewRules.UnknownProject(review, new HashSet<string>(["prj_1", "prj_2"], StringComparer.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_diff_names_the_review_keys_that_change()
    {
        var live = ProfileRules.Check(_valid).Value;

        Assert.Equal(["review.login"], ProfileDiff.Between(live, With(new PrReviewSettings(Login: "joi-example"))));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_error_codes_are_in_the_catalogue()
    {
        var codes = SindriErrors.All.Select(static e => e.Code).ToList();

        Assert.All(_codes, c => Assert.Contains(c, codes, StringComparer.Ordinal));
    }
}
