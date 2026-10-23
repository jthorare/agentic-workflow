namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The T3 server Sindri talks to.</summary>
/// <param name="Id">Its environment id (part of a thread's app link).</param>
/// <param name="Version">Its version.</param>
internal sealed record T3Environment(string Id, string Version);
