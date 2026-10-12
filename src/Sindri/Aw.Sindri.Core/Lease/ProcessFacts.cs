namespace Aw.Sindri.Core.Lease;

/// <summary>What the operating system says about a pid on this machine.</summary>
/// <param name="Running">Whether a process with that pid exists.</param>
/// <param name="StartedAt">When it started (whole seconds, UTC), when it exists and the start time is readable.</param>
public sealed record ProcessFacts(bool Running, DateTimeOffset? StartedAt);
