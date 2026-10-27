using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// One 'FillInput' call (plan decision 1). It carries what the workflow already holds, which is already in history: the
/// resolved-by-the-activity 'with:' values and the facts' stored references, never fact content.
/// </summary>
/// <param name="Address">The instance address.</param>
/// <param name="State">The state whose Step runs.</param>
/// <param name="Run">The run token.</param>
/// <param name="Index">The input's 1-based place in the Step's list.</param>
/// <param name="Input">The input block.</param>
/// <param name="Step">The Step block.</param>
/// <param name="With">The input's 'with:' arguments as the recipe wrote them.</param>
/// <param name="Facts">The instance's facts.</param>
/// <param name="Repo">The repo the instance's item belongs to; null when it has none.</param>
internal sealed record FillInputRequest(
    string Address,
    string State,
    string Run,
    int Index,
    BlockId Input,
    BlockId Step,
    IReadOnlyDictionary<string, WithValue> With,
    IReadOnlyDictionary<FactKey, FactValue> Facts,
    string? Repo)
{
    /// <summary>What the input fills from: the arguments resolved against the facts (a '{ profile }' one as its reference), the item's repo and the instance address.</summary>
    /// <returns>The input's context.</returns>
    internal StepInputContext Context() => new(Input, Step, EffectContexts.InputArguments(With, Facts), EffectContexts.Elements(Facts), Repo is { } repo && RepoName.TryFrom(repo, out var name) ? name : null, Instance: Address);
}
