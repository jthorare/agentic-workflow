namespace Aw.Forge.Core.Pulls;

/// <summary>One check run, or one commit status read as a run, on a commit.</summary>
/// <param name="Name">The check's name (a status's context).</param>
/// <param name="App">The app that reported it; null for a commit status.</param>
/// <param name="Conclusion">The forge's conclusion (success, failure, neutral, skipped, cancelled, timed_out, action_required), or "pending" while it has none. A status's error reads as failure.</param>
/// <param name="Sha">The commit it ran on.</param>
public sealed record CheckRun(string Name, string? App, string Conclusion, string Sha);
