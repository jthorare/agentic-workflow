using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A Step run that a report completes (spec §2). The engine (P3) adds the run token and the loop round.</summary>
/// <param name="Step">The Step's id.</param>
/// <param name="Facts">The instance's facts when the run started.</param>
public sealed record StepRun(BlockId Step, IReadOnlyDictionary<FactKey, JsonElement> Facts);
