using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>One Step registered with AddBlockStep.</summary>
/// <param name="Step">The Step's id.</param>
public sealed record BlockStepRegistration(BlockId Step);
