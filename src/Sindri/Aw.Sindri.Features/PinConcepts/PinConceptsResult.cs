using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.PinConcepts;

/// <summary>What a PinConcepts job wrote (concept pins spec §3.6, the CLI's '--json').</summary>
/// <param name="Pins">Each pin it touched, in request order: sets, then removals.</param>
/// <param name="Hash">The live profile's hash after the write: the one to approve.</param>
/// <param name="Approved">Always false: the job never approves.</param>
/// <param name="Dropped">The folder rules a removal dropped, as 'id.key' ('billing.paths').</param>
public sealed record PinConceptsResult(IReadOnlyList<PinWritten> Pins, ProfileHash Hash, bool Approved, IReadOnlyList<string> Dropped);
