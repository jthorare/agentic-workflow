namespace Aw.Sindri.Ui.Api.Memory;

/// <summary>GET /sindri/memories.</summary>
/// <param name="Memories">The stores.</param>
internal sealed record MemoryList(IReadOnlyList<MemoryView> Memories);
