using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One run of a block-runtime Step (DT spec §8.2): what the 'RunBlockStep' activity is given.</summary>
/// <param name="Address">The instance address.</param>
/// <param name="State">The state whose Step runs.</param>
/// <param name="Token">The run token '{entry}.{n}'.</param>
/// <param name="Step">The Step's block id.</param>
/// <param name="Arguments">Its 'with:' arguments, as the recipe wrote them.</param>
/// <param name="Facts">The instance's facts as stored references; the activity reads the content the Step names, so no fact content enters the workflow's history.</param>
/// <param name="Repo">The item's repo name, for the per-repo profile keys, or null.</param>
internal sealed record BlockStepRun(string Address, string State, string Token, BlockId Step, IReadOnlyDictionary<string, WithValue> Arguments, IReadOnlyDictionary<FactKey, FactValue> Facts, string? Repo = null);
