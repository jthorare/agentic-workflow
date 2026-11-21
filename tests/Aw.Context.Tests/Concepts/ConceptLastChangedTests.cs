using Aw.Context.Core.Concepts;
using Aw.Context.Core.Map;

namespace Aw.Context.Tests.Concepts;

/// <summary>ConceptRow.LastChanged: the newest change time among the code files of the concept's features, null when git knows none of them (#3760).</summary>
public sealed class ConceptLastChangedTests
{
    private static readonly DateTimeOffset _old = new(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _mid = new(2026, 3, 2, 12, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _new = new(2026, 11, 20, 8, 15, 0, TimeSpan.Zero);

    private static ConceptsView Read(IReadOnlyDictionary<string, DateTimeOffset>? changed) =>
        ConceptBuilder.Build(ConceptGraph.Basis, new ConceptsQuery(MapRead.Current, new ConceptPage()), ConceptGraph.Product().Build(), ConceptGraph.Text, pins: null, changed).Value;

    [Fact, Trait("Lane", "unit")]
    public void Concepts_carry_the_newest_file_change_time()
    {
        var changed = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal) { ["src/Shifts.cs"] = _mid, ["src/Shared.cs"] = _new, ["src/Invoices.cs"] = _old };

        var view = Read(changed);

        Assert.Equal(_new, view.Concepts.Single(c => c.Id.Value == "concept:scheduling").LastChanged);
        Assert.Equal(_old, view.Concepts.Single(c => c.Id.Value == "concept:billing").LastChanged);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_with_no_known_time_is_skipped_and_a_concept_with_none_known_has_null()
    {
        var changed = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal) { ["src/Shifts.cs"] = _mid, ["src/Elsewhere.cs"] = _new };

        var view = Read(changed);

        Assert.Equal(_mid, view.Concepts.Single(c => c.Id.Value == "concept:scheduling").LastChanged);
        Assert.Null(view.Concepts.Single(c => c.Id.Value == "concept:billing").LastChanged);
        Assert.All(Read(changed: null).Concepts, c => Assert.Null(c.LastChanged));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_code_paths_to_ask_git_for_are_the_distinct_files_of_the_page_concepts()
    {
        var paths = ConceptBuilder.CodePaths(ConceptGraph.Basis, new ConceptsQuery(MapRead.Current, new ConceptPage()), ConceptGraph.Product().Build());

        Assert.Equal(["src/Invoices.cs", "src/Shared.cs", "src/Shifts.cs"], paths);
    }
}
