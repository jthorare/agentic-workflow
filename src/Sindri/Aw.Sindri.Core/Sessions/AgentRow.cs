using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Sessions;

/// <summary>
/// One row of the Agents page: one live session, one waiting job or one lane with no session (Agents page spec §3.1). It carries the fields the page adds to today's
/// <see cref="SessionRow"/>; the list route joins the two (A1).
/// </summary>
/// <param name="Id">'session:&lt;provider&gt;/&lt;id&gt;', 'job:&lt;pid&gt;' or 'lane:&lt;team&gt;/&lt;lane&gt;'. The ids match Home's.</param>
/// <param name="Version">Starts at 1 and rises by 1 per change; every steer names the version it read (SND-AGENT-002).</param>
/// <param name="State">The row's own state, never the reason it is listed.</param>
/// <param name="StateSource">Where the state came from.</param>
/// <param name="StateReadAt">When the state was read.</param>
/// <param name="Estimated">True when the state is a guess from the transcript's age.</param>
/// <param name="Title">The work in words: lane name, then the PR or issue title, then 'repo · branch', then the provider and start time.</param>
/// <param name="Facts">Lane, branch, PR or issue, context, heavy slot.</param>
/// <param name="Reasons">The reasons that hold now (<see cref="AgentReasons.Evaluate"/>); the first is the primary.</param>
/// <param name="Parent">The lead's id, for a builder; null otherwise.</param>
/// <param name="Since">When the state began.</param>
/// <param name="LastActivity">When the transcript last grew; null when it has no transcript.</param>
/// <param name="Blocks">Home's 'blocks N' for the row, when Home's rank exists; null otherwise. Display only.</param>
/// <param name="Actions">The op ids that apply (spec §5); the first is the primary.</param>
/// <param name="Links">What the row links to: 'home:&lt;row id&gt;', 'pr:&lt;number&gt;', 'issue:&lt;number&gt;', 'run:&lt;id&gt;', 'node:&lt;run&gt;/&lt;node&gt;' or 'launch:&lt;id&gt;'.</param>
public sealed record AgentRow(
    AgentRowId Id, long Version, AgentState State, AgentStateSource StateSource, DateTimeOffset StateReadAt, bool Estimated, string Title, AgentFacts Facts,
    IReadOnlyList<AgentReason> Reasons, string? Parent, DateTimeOffset Since, DateTimeOffset? LastActivity, int? Blocks, IReadOnlyList<string> Actions, IReadOnlyList<string> Links);
