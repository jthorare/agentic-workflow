using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>What the Steps that write the scope map and acceptance list are told (#1112): the schema files, the allowed top-level fields, and that answers live in the answer log.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeMapInstructionsTests
{
    private static string All => string.Join('\n', new ScopeMapInstructions().Lines);

    [Theory]
    [InlineData("step.scope.draft", true)]
    [InlineData("step.interview.apply", true)]
    [InlineData("step.scope.check", false)]
    [InlineData("step.scope.challenge", false)]
    public void The_instructions_are_for_the_draft_and_apply_Steps_only(string step, bool expected) =>
        Assert.Equal(expected, new ScopeMapInstructions().IsFor(BlockId.From(step)));

    [Theory]
    [InlineData("schemas/scoping/scope-map.json")]
    [InlineData("schemas/scoping/acceptance.json")]
    [InlineData("surfaces, implications, workstreams, flagged, excluded, unmapped, unresolved")]
    [InlineData("fact:answerLog")]
    [InlineData("scopeMap.implications")]
    public void The_lines_name_the_schemas_the_allowed_fields_and_where_answers_go(string text) =>
        Assert.Contains(text, All, StringComparison.Ordinal);
}
