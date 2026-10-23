namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>What the T3 host needs to know about a thread.</summary>
/// <param name="Status">'idle', 'running', 'waiting', 'completed', 'failed', 'interrupted', ….</param>
/// <param name="Archived">True once the user archived it: the session is over.</param>
internal sealed record T3ThreadState(string Status, bool Archived);
