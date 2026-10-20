namespace Aw.Sindri.Core.Line;

/// <summary>What an evidence Step needs besides the files: the packet's identity and the hashes of the packets it follows, which the instance's facts hold at run time and a desk caller passes.</summary>
/// <param name="Identity">The packet's id, repository and base commit; null when the caller gave none.</param>
/// <param name="ScopeHash">The frozen scope packet's hash; null when the caller gave none.</param>
/// <param name="DesignHash">The design packet's hash (a plan's first parent); null when the caller gave none.</param>
/// <param name="Parallel">workflow.line.parallel, which graph.json is derived with.</param>
/// <param name="MaxStack">workflow.line.maxStack, recorded in graph.json.</param>
/// <param name="DefaultBranch">The branch a phase with no dependency starts from.</param>
public sealed record LineDeskContext(PacketIdentity? Identity = null, string? ScopeHash = null, string? DesignHash = null, int Parallel = 2, int MaxStack = 4, string DefaultBranch = "main");
