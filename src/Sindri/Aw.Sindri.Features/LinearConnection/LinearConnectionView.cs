namespace Aw.Sindri.Features.LinearConnection;

/// <summary>What Settings, Connections, Linear shows (Linear hub spec 2026-11-04 §3).</summary>
/// <param name="Read">The read key.</param>
/// <param name="Write">The write key.</param>
/// <param name="Teams">The teams the live profile picks.</param>
/// <param name="Pending">True when the live profile differs from the approved one in a `context.sources.linear` key, so the connection waits for an approval.</param>
internal sealed record LinearConnectionView(LinearKeyStateView Read, LinearKeyStateView Write, IReadOnlyList<LinearConnectionTeamView> Teams, bool Pending);
