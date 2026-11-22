using Aw.Context.Core.Concepts;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.Concepts;

/// <summary>The concept answer store's contract (issue 3676): the fake and the SQLite store pass the same tests, each over the graph store its basis reads.</summary>
public abstract class ConceptAnswerStoreContract
{
    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>An empty store names no basis, so every read builds.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_empty_store_has_no_basis()
    {
        const string test = nameof(An_empty_store_has_no_basis);
        var (_, answers) = Create();

        Expect.That(Expect.Ok(await answers.Basis(MapRead.Current, None), test) is null, test, "a basis in an empty store");
        Expect.That(Expect.Ok(await answers.Basis(MapRead.NewestDraft, None), test) is null, test, "a draft basis in an empty store");
    }

    /// <summary>A kept answer reads back whole by its key and kind, and no other key or kind reads it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_kept_answer_reads_back_by_its_key_and_kind_only()
    {
        const string test = nameof(A_kept_answer_reads_back_by_its_key_and_kind_only);
        var (graphs, answers) = Create();
        var saved = Expect.Ok(await graphs.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        var basis = Expect.Ok(await answers.Basis(MapRead.Current, None), test);

        Expect.That(basis?.Version == saved.Number, test, "the basis doesn't name the current version");
        _ = Expect.Ok(await answers.Keep("k1", basis!, Concepts("v1"), None), test);
        _ = Expect.Ok(await answers.Keep("k2", basis!, Glossary("v1"), None), test);
        var concepts = Expect.Ok(await answers.Concepts("k1", None), test);
        var glossary = Expect.Ok(await answers.Glossary("k2", None), test);

        Expect.That(concepts is { MapVersion: "v1", State: ConceptStates.Ok, Concepts: [{ Name: "Billing", Pinned: true, Terms: ["invoice"] }], Other.Name: "Other", Next: "c2" } && concepts.PagesInNoConcept == 3, test, "the concepts answer changed on the way back");
        Expect.That(glossary is { MapVersion: "v1", Concepts: [{ Terms: ["invoice", "refund"], OwnerTerms: ["invoice"] }], Terms: [{ Term: "invoice" }] }, test, "the glossary answer changed on the way back");
        Expect.That(Expect.Ok(await answers.Concepts("k3", None), test) is null, test, "a key nobody kept read an answer");
        Expect.That(Expect.Ok(await answers.Glossary("k1", None), test) is null, test, "a concepts row read as a glossary");
    }

    /// <summary>The basis moves with every input a version's answer reads: a new version, an appended override; a retained version keeps its own number.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_basis_moves_with_a_new_version_and_an_appended_override()
    {
        const string test = nameof(The_basis_moves_with_a_new_version_and_an_appended_override);
        var (graphs, answers) = Create();
        var first = Expect.Ok(await graphs.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        var before = Expect.Ok(await answers.Basis(MapRead.Current, None), test);
        _ = Expect.Ok(await graphs.AppendOverrides([new MapOverride(OverrideAction.Rename, NodeKey.From("feature:tabs"), "avery", SampleMap.At, "Renamed")], None), test);
        var overridden = Expect.Ok(await answers.Basis(MapRead.Current, None), test);
        var second = Expect.Ok(await graphs.SaveCurrent(SampleMap.Build(" again"), SampleMap.At, None), test);
        var current = Expect.Ok(await answers.Basis(MapRead.Current, None), test);
        var retained = Expect.Ok(await answers.Basis(new MapRead(Draft: false, new MapVersionRef(first.Number, Commit: null)), None), test);

        Expect.That(before is not null && overridden is not null && before.Overrides != overridden.Overrides && before.Version == overridden.Version, test, "an appended override didn't move the basis");
        Expect.That(current?.Version == second.Number && current.Version != first.Number, test, "a new current version kept the old basis");
        Expect.That(retained?.Version == first.Number, test, "a retained version read as another");
        Expect.That(Expect.Ok(await answers.Basis(new MapRead(Draft: false, new MapVersionRef(second.Number + 5, Commit: null)), None), test) is null, test, "a version the store never held has a basis");
    }

    /// <summary>A version that is deleted takes its kept answers with it at the next write.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_deleted_version_takes_its_answers_with_it()
    {
        const string test = nameof(A_deleted_version_takes_its_answers_with_it);
        var (graphs, answers) = Create();
        _ = Expect.Ok(await graphs.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        var old = Expect.Ok(await answers.Basis(MapRead.Current, None), test);
        _ = Expect.Ok(await answers.Keep("old", old!, Concepts("v1"), None), test);
        _ = Expect.Ok(await graphs.SaveCurrent(SampleMap.Build(" again"), SampleMap.At, None), test);
        _ = Expect.Ok(await graphs.Delete([old!.Version], None), test);
        var now = Expect.Ok(await answers.Basis(MapRead.Current, None), test);
        _ = Expect.Ok(await answers.Keep("new", now!, Concepts("v2"), None), test);

        Expect.That(Expect.Ok(await answers.Concepts("old", None), test) is null, test, "a deleted version's answer is still kept");
        Expect.That(Expect.Ok(await answers.Concepts("new", None), test)?.MapVersion == "v2", test, "the new version's answer isn't kept");
    }

    private static ConceptsView Concepts(string version) =>
        new(1, Repo: null, version, ConceptStates.Ok, Code: null,
            [new ConceptRow(NodeKey.From("concept:billing"), "Billing", "Charges and refunds.", "derived", new ConceptSize(2, 1, 0, 4), [new ConceptCitation("repo:web/docs/billing.md")]) { Terms = ["invoice"], Pinned = true, Pages = 0 }],
            new ConceptOther(NodeKey.From("concept:other"), "Other", new ConceptSize(1, 0, 0, 0)), [new ConceptLink(NodeKey.From("concept:billing"), NodeKey.From("concept:other"), 2)], Next: "c2") { PagesInNoConcept = 3 };

    private static ConceptGlossaryView Glossary(string version) =>
        new(1, Repo: null, version, ConceptStates.Ok, [new ConceptTermsRow(NodeKey.From("concept:billing"), "Billing", ["invoice", "refund"]) { OwnerTerms = ["invoice"] }],
            [new TermConceptsRow("invoice", [NodeKey.From("concept:billing")]), new TermConceptsRow("refund", [NodeKey.From("concept:billing")])]);

    /// <summary>A fresh graph store and the answer store over it.</summary>
    /// <returns>The pair.</returns>
    protected abstract (IGraphStore Graphs, IConceptAnswerStore Answers) Create();
}
