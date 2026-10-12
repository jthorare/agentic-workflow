using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Tests.Workflow;

public sealed class RecipeFindingTests
{
    private static readonly SourcePositions _positions = new(new Dictionary<string, SourcePosition>(StringComparer.Ordinal)
    {
        [""] = new(1, 1),
        ["states"] = new(5, 1),
        ["states.orphan"] = new(14, 3),
        ["transitions"] = new(17, 1),
        ["transitions[0]"] = new(18, 5),
    });

    [Fact, Trait("Lane", "unit")]
    public void A_finding_prints_rule_what_where_path_and_fix()
    {
        var finding = new RecipeSource("RCP-REACH-001.yaml", _positions).Finding(
            "RCP-REACH-001", "states.orphan", "state 'orphan' is unreachable from 'produce'", "add a transition into it or remove it");

        Assert.Equal(
            "RCP-REACH-001: state 'orphan' is unreachable from 'produce' at RCP-REACH-001.yaml:14:3 (states.orphan); add a transition into it or remove it",
            finding.ToLine());
        Assert.Equal(FindingSeverity.Error, finding.Severity);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("states.orphan", 14, 3)]
    [InlineData("states.orphan.outcome", 14, 3)]
    [InlineData("transitions[0].gates[1].use", 18, 5)]
    [InlineData("transitions[3]", 17, 1)]
    [InlineData("loops.revisions", 1, 1)]
    public void A_node_not_in_the_file_is_located_at_its_nearest_recorded_ancestor(string path, int line, int column) =>
        Assert.Equal(new SourcePosition(line, column), _positions.Locate(path));

    [Fact, Trait("Lane", "unit")]
    public void Without_positions_everything_is_at_the_start() =>
        Assert.Equal(SourcePosition.Start, SourcePositions.None.Locate("states.produce"));

    [Fact, Trait("Lane", "unit")]
    public void Only_errors_fail_validation()
    {
        var source = new RecipeSource("r.yaml", SourcePositions.None);

        Assert.True(RecipeValidator.Passes(
        [
            source.Finding("RCP-RES-001", "states.wait", "no allocator for 'heavy'", "it parks until one exists", FindingSeverity.Information),
            source.Finding("RCP-EFFECT-001", "transitions[0]", "no effect allowlist", "add workflow.effects", FindingSeverity.Skipped),
        ]));
        Assert.False(RecipeValidator.Passes([source.Finding("RCP-INIT-001", "initial", "no such state", "name a state")]));
    }
}
