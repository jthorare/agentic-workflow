using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.SetPages;

/// <summary>What a SetPages job wrote (pages spec §3.1, the CLI's '--json').</summary>
/// <param name="Pages">Each page it touched, in request order: sets, then removals.</param>
/// <param name="Hash">The live profile's hash after the write: the one to approve (the live hash read, on a dry run).</param>
/// <param name="Written">False on a dry run.</param>
/// <param name="Approved">Always false: the job never approves.</param>
/// <param name="Diff">The page rows that change between the live profile and the planned one, as 'key: old -> new'.</param>
public sealed record SetPagesResult(IReadOnlyList<PageWritten> Pages, ProfileHash Hash, bool Written, bool Approved, IReadOnlyList<string> Diff);
