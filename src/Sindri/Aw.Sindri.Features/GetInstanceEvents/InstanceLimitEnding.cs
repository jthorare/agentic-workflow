namespace Aw.Sindri.Features.GetInstanceEvents;

/// <summary>How a sandbox limit ended an agent run (sandbox spec §8): which limit, its value, how long the session ran and the branch that kept its work.</summary>
/// <param name="Kind">'memory' or 'wall'.</param>
/// <param name="Limit">The limit as the profile writes it ('12GiB', '40m').</param>
/// <param name="Elapsed">Seconds from the session's start to its end.</param>
/// <param name="Branch">The run branch when it has commits beyond its base (kept); null otherwise.</param>
public sealed record InstanceLimitEnding(string Kind, string Limit, string Elapsed, string? Branch);
