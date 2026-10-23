namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>One session the T3 host started: its name, its thread and the environment the thread lives in (for the app link).</summary>
/// <param name="Name">The session name ('snd-&lt;launchId&gt;').</param>
/// <param name="ThreadId">The T3 thread.</param>
/// <param name="EnvironmentId">The T3 environment.</param>
internal sealed record T3SessionEntry(string Name, string ThreadId, string EnvironmentId);
