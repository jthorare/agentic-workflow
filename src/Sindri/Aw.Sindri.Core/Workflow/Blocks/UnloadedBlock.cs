using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A block this build knows by id but could not load, and why (a skill block whose manifest failed its checks; skill flows spec §3.2). A recipe naming it fails validation with the reason, not with the plain "isn't registered".</summary>
/// <param name="Id">The block id ('step.skill.review').</param>
/// <param name="Reason">Why it did not load, in plain words.</param>
public sealed record UnloadedBlock(BlockId Id, string Reason);
