using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>How long one Step input took to fill (a 'workflow.injection' row's 'inputs', in declared order).</summary>
/// <param name="Input">The input block.</param>
/// <param name="DurationMs">The fill's wall-clock milliseconds, as the activity measured them.</param>
public sealed record InputTiming(BlockId Input, long DurationMs);
