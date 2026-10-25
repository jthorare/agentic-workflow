using Aw.Context.Core.Engine;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'index.concepts' (docs/csharp/sindri-profile.md): concepts the owner pins, the profile's and a repo's own, handed to the context engine.</summary>
public sealed class IndexConceptsTests : IDisposable
{
    private const string Base = "schemaVersion: 1\nuser: thor\n";
    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SindriProfile Profile(IndexSettings? top = null, IndexSettings? repo = null) =>
        new(1, "thor", Repos: [new RepoEntry("web", "/work/web", repo)], Index: top);

    private static IndexSettings Pins(params ConceptSettings[] concepts) => new(Concepts: concepts);

    [Fact, Trait("Lane", "integration")]
    public async Task The_yaml_profile_reads_index_concepts()
    {
        _files.Write(Base + "index:\n  concepts:\n    - id: scheduling\n      name: Self scheduling\n      summary: Shifts and swaps\n      features: [\"feature:shifts\", \"feature:shifts.*\"]\n      paths: [\"apps/web/src/Schedule/**\"]\nrepos:\n  - { name: web, path: /work/web }\n");

        var profile = (await _files.Source.Load(Token)).Value.Profile;

        var pin = Assert.Single(profile.Index!.Concepts!);
        Assert.Equal("scheduling", pin.Id!.Value.Value);   // why: the pin names an id
        Assert.Equal("Self scheduling", pin.Name);
        Assert.Equal("Shifts and swaps", pin.Summary);
        Assert.Equal(["feature:shifts", "feature:shifts.*"], pin.Features, StringComparer.Ordinal);
        Assert.Equal(["apps/web/src/Schedule/**"], pin.Paths, StringComparer.Ordinal);
        Assert.True(ProfileRules.Check(profile).IsSuccess);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("Bad Id")]
    [InlineData("Scheduling")]
    [InlineData("-lead")]
    [InlineData(".lead")]
    [InlineData("tabs.")]
    [InlineData("tabs..manager")]
    public async Task An_id_that_is_not_a_slug_is_refused_with_its_key_path(string id)
    {
        _files.Write(Base + "index:\n  concepts:\n    - { id: " + id + ", name: Billing }\nrepos:\n  - { name: web, path: /work/web }\n");

        var loaded = await _files.Source.Load(Token);

        Assert.True(loaded.IsError);
        Assert.Contains("index.concepts[0].id", loaded.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("tabs.tab-manager")]
    [InlineData("billing.invoices")]
    public void A_pin_may_name_a_derived_id_with_a_dot(string id)
    {
        var profile = Profile(Pins(new ConceptSettings(ConceptPinId.From(id), "Tab manager")));

        Assert.True(ProfileRules.Check(profile).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repos_concepts_are_added_to_the_profiles()
    {
        var profile = Profile(Pins(new ConceptSettings(Name: "Billing")), Pins(new ConceptSettings(Name: "Scheduling")));

        var concepts = RepoConcepts.For(profile, profile.Repos![0]);

        Assert.Equal(["Billing", "Scheduling"], concepts.Select(c => c.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_pins_reach_the_context_repo()
    {
        var profile = Profile(Pins(new ConceptSettings(ConceptPinId.From("billing"), "Billing", Features: ["feature:invoices.*"])), Pins(new ConceptSettings(Name: "Scheduling", Paths: ["src/schedule/**"])));

        var repo = ScopeRules.Usable(ContextScopes.Entries(profile)).Single();

        Assert.Equal(
            ["billing|Billing|feature:invoices.*|", "|Scheduling||src/schedule/**"],
            repo.Concepts.Select(c => string.Join('|', c.Id?.Value, c.Name, string.Join(',', c.Features), string.Join(',', c.Paths))),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_without_pins_has_none() =>
        Assert.Empty(ScopeRules.Usable(ContextScopes.Entries(Profile())).Single().Concepts);

    [Fact, Trait("Lane", "unit")]
    public void The_pins_reach_the_context_repo_through_the_profile_entry()
    {
        var pin = new ConceptPin(ConceptId.From("billing"), "Billing", Summary: null, Features: [], Paths: []);

        Assert.Equal([pin], ScopeRules.Usable([new ProfileRepoEntry("web", "/r/web", DenyPaths: null, Concepts: [pin])]).Single().Concepts);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, "billing", "index.concepts[0].name: must not be empty")]
    [InlineData("  ", "billing", "index.concepts[0].name: must not be empty")]
    [InlineData("Billing", "other", "index.concepts[0].id: 'other' is reserved")]
    public void A_pin_that_cant_be_one_names_its_key_path(string? name, string id, string message)
    {
        var error = ProfileRules.Check(Profile(Pins(new ConceptSettings(ConceptPinId.From(id), name)))).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Duplicate_ids_are_refused_in_a_list_and_between_the_profile_and_a_repo()
    {
        var twice = Profile(Pins(new ConceptSettings(ConceptPinId.From("billing"), "Billing"), new ConceptSettings(ConceptPinId.From("billing"), "Invoices")));
        var across = Profile(Pins(new ConceptSettings(ConceptPinId.From("billing"), "Billing")), Pins(new ConceptSettings(ConceptPinId.From("billing"), "Invoices")));

        Assert.Contains("index.concepts[1].id: 'billing' is already used", ProfileRules.Check(twice).Error.Message, StringComparison.Ordinal);
        Assert.Contains("repos.web.index.concepts[0].id: 'billing' is already used", ProfileRules.Check(across).Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("shifts", "index.concepts[0].features[0]: must be a feature: key or glob")]
    [InlineData("record:abc", "index.concepts[0].features[0]: must be a feature: key or glob")]
    [InlineData("feature:", "index.concepts[0].features[0]: must be a feature: key or glob")]
    [InlineData("feature:a b", "index.concepts[0].features[0]: must be a feature: key or glob")]
    public void A_features_entry_that_is_not_a_feature_key_or_glob_is_refused(string feature, string message)
    {
        var error = ProfileRules.Check(Profile(Pins(new ConceptSettings(Name: "Billing", Features: [feature])))).Error;

        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bad_path_glob_names_the_repo_and_good_pins_pass()
    {
        var bad = Profile(repo: Pins(new ConceptSettings(Name: "Billing", Paths: ["ok/**", "../x"])));
        var good = Profile(Pins(new ConceptSettings(ConceptPinId.From("billing"), "Billing", "Invoices", ["feature:invoices", "feature:invoices.*"], ["src/billing/**"]), new ConceptSettings(Name: "Empty")));

        Assert.Contains("repos.web.index.concepts[0].paths[1]: must not contain '..'", ProfileRules.Check(bad).Error.Message, StringComparison.Ordinal);
        Assert.True(ProfileRules.Check(good).IsSuccess);
    }
}
