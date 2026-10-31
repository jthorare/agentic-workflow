namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>What the start-up sweep of a dead launch's sandbox files asks of the box, so a test can answer instead.</summary>
/// <param name="Mac">Whether to sweep the macOS temp folders too (the group files go on every OS).</param>
/// <param name="TempRoot">Where the macOS per-launch temp folders sit.</param>
/// <param name="Owned">Whether this user owns a folder; null asks the system (stat).</param>
/// <param name="Alive">Whether a process group has a process; null asks the system.</param>
internal sealed record StaleSweepSeams(bool Mac = false, string TempRoot = "/private/tmp", Func<string, bool>? Owned = null, Func<int, bool>? Alive = null);
