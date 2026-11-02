using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>One denial seen in a run (sandbox spec §3.6): by the egress proxy, in the transcript tail, or in the per-launch fail-open log.</summary>
/// <param name="Kind">Network, policy or path.</param>
/// <param name="Target">A host, a path or a tool name; never a whole command line.</param>
/// <param name="Rule">The rule id that denied it (see <see cref="DenialRules"/>).</param>
public sealed record DenialObservation(DenialKind Kind, string Target, string Rule);
