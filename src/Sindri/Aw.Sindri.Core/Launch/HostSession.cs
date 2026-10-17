namespace Aw.Sindri.Core.Launch;

/// <summary>What <see cref="ISessionHost.Start"/> runs.</summary>
/// <param name="Name">The host's session name ('snd-&lt;launchId&gt;').</param>
/// <param name="WorkDir">The directory the agent starts in.</param>
/// <param name="Argv">The command line; the first element is the executable ('sindri session wrap … -- claude …').</param>
/// <param name="Env">The environment the session gets, set through the host ('new-session -e'), never on a command line.</param>
public sealed record HostSession(string Name, string WorkDir, IReadOnlyList<string> Argv, IReadOnlyDictionary<string, string> Env);
