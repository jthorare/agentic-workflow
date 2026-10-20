using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Plan;

/// <summary>Run a plan block Step's code over the files a caller holds and return the report a desk 'complete' takes (line spec §6.1, Appendix D).</summary>
/// <param name="Block">The Step: 'step.plan.check' or 'step.plan.evidence', or the short 'plan.check' and 'plan.evidence'.</param>
/// <param name="Facts">The files as <see cref="PlanCheckRequest"/> names them.</param>
/// <param name="Context">The packet's identity, the design and scope packets' hashes and the profile values graph.json is derived with.</param>
public sealed record PlanDeskRequest(string Block, IReadOnlyDictionary<string, string> Facts, LineDeskContext Context);
