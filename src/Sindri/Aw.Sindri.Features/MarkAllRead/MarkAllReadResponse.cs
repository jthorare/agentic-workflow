namespace Aw.Sindri.Features.MarkAllRead;

/// <summary>How many pointers moved (messages spec §8.2). A local write: no ledger event, so no sequence.</summary>
/// <param name="Moved">The pointers moved.</param>
public sealed record MarkAllReadResponse(int Moved);
