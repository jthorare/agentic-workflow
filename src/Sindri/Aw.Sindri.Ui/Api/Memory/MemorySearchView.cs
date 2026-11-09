namespace Aw.Sindri.Ui.Api.Memory;

/// <summary>GET /sindri/memories/{name}/search.</summary>
/// <param name="Facts">The matching facts, best first.</param>
/// <param name="Count">How many came back.</param>
/// <param name="Mode">How they were found ('semantic' or 'keyword').</param>
/// <param name="Note">A sentence about the mode (why it fell back to keyword), or null.</param>
internal sealed record MemorySearchView(IReadOnlyList<MemoryFactView> Facts, int Count, string Mode, string? Note);
