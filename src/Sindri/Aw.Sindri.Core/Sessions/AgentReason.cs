namespace Aw.Sindri.Core.Sessions;

/// <summary>One reason an Agents row is listed first, or in Waiting (Agents page spec §3.1, §3.2). A reason holds while its trigger holds.</summary>
/// <param name="Class">Its place in the order, 1 to 7 (spec §4.1). Classes 2 and 6 place a row in Waiting; every other class places it in Needs you.</param>
/// <param name="Kind">What the reason is.</param>
/// <param name="Word">The chip word, such as "asking", "recycle" or "back at 14:00".</param>
/// <param name="Text">The words on the row, such as "qa: asking to run Bash".</param>
/// <param name="Since">When the row first held this reason, from its source and not from the poll.</param>
/// <param name="Source">Where the facts behind the reason came from.</param>
public sealed record AgentReason(int Class, AgentReasonKind Kind, string Word, string Text, DateTimeOffset Since, AgentStateSource Source);
