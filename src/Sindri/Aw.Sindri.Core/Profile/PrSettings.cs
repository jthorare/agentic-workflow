using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile's 'workflow.pr:' block (PR babysitter spec W27): who counts as a trusted bot, how the babysitter fixes and
/// polls, how replies and merges go. A key left out takes the default named on each parameter.
/// </summary>
/// <param name="Bots">The allowlisted bot logins (Q2); any other bot is untrusted.</param>
/// <param name="AutofixCap">The most CI fixes pushed for one PR, 1 to 5 (default 5).</param>
/// <param name="PollInterval">How often the PR watcher polls the forge, a delay such as '60s' (default 60s, 5s to 24h).</param>
/// <param name="RemindAfter">How long a PR waits at ready-to-merge before the desk is reminded, a duration (default 14d).</param>
/// <param name="Replies">'desk' (the owner sends each reply) or 'post' (Sindri posts it, with a footer); default desk (Q5).</param>
/// <param name="MergeMethod">'squash', 'merge' or 'rebase' (default squash, Q11).</param>
/// <param name="HeldBlocksMerge">Whether held comments refuse the merge (default false, Q13).</param>
/// <param name="DocsMergeWait">The longest the stacked docs PR is awaited after the merge, a duration (default 30m).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Bots = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? AutofixCap = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PollInterval = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RemindAfter = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Replies = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MergeMethod = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? HeldBlocksMerge = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DocsMergeWait = null);
