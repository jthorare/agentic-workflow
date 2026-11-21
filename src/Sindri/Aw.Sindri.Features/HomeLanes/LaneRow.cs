using Aw.Sindri.Core.NeedsYou;

namespace Aw.Sindri.Features.HomeLanes;

/// <summary>A lane row Home should show.</summary>
/// <param name="Kind">'lane-recycle', 'agent-limited' or 'pr-ready-merge'.</param>
/// <param name="Key">The lane, subject or PR number the row is about.</param>
/// <param name="Title">The headline.</param>
/// <param name="Reason">Why it needs the owner.</param>
internal sealed record LaneRow(NeedsYouKind Kind, string Key, string Title, string Reason);
