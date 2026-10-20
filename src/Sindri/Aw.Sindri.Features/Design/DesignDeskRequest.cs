using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

/// <summary>Run a design block Step's code over the files a caller holds and return the report a desk 'complete' takes (line spec §4.1, Appendix D).</summary>
/// <param name="Block">The Step: 'step.design.check' or 'step.design.evidence', or the short 'design.check' and 'design.evidence'.</param>
/// <param name="Facts">The files as <see cref="DesignCheckRequest"/> names them.</param>
/// <param name="Context">The packet's identity and the scope packet's hash, which the evidence Step assembles with.</param>
public sealed record DesignDeskRequest(string Block, IReadOnlyDictionary<string, string> Facts, LineDeskContext Context);
