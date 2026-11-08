namespace Aw.Sindri.Core.Launch;

/// <summary>One finished check: when it ran and what it found.</summary>
/// <param name="At">When it finished.</param>
/// <param name="Report">What it found.</param>
public sealed record SandboxCheckRecord(DateTimeOffset At, SandboxCheckReport Report);
