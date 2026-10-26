using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.relevance-apply' on the block runtime (spec 4.3): <see cref="RelevanceApply"/> over the lenses, the coverage, the brief and the two label sets. It rewrites 'fact:lenses' and 'fact:coverage' without the
/// entries both reviewers called off topic, and writes 'fact:relevance' and 'fact:mapFixes'. A verdict or ruling for an id the lenses do not hold, or an id twice, fails the Step with the fix line.
/// </summary>
internal sealed class RelevanceApplyStep : IBlockStep, IBlockStepId
{
    private const string Name = "step.scope.relevance-apply";

    private static readonly string[] _reads = ["fact:lenses", "fact:coverage", "fact:brief", "fact:relevanceVerdicts", "fact:relevanceRulings"];

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From(Name);

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Missing(context, _reads) is { Count: > 0 } missing)
        {
            return ValueTask.FromResult(BlockStepResult.Failed($"{Name} needs {string.Join(", ", missing)}, which the instance does not hold"));
        }

        var applied = RelevanceApply.Apply(Fact(context, "fact:lenses"), Fact(context, "fact:coverage"), Fact(context, "fact:brief"), Fact(context, "fact:relevanceVerdicts"), Fact(context, "fact:relevanceRulings"));
        return ValueTask.FromResult(applied.IsError
            ? BlockStepResult.Failed(applied.Error.Message)
            : StepFacts.Done("applied", ("fact:lenses", applied.Value.Lenses), ("fact:coverage", applied.Value.Coverage), ("fact:relevance", applied.Value.Relevance), ("fact:mapFixes", applied.Value.MapFixes)));
    }

    private static JsonNode Fact(StepContext context, string key) => StepFacts.Read(context, key) ?? new JsonObject();
}
