namespace Aw.Sindri.Ui.Api.Memory;

/// <summary>POST /sindri/memories: the new store.</summary>
/// <param name="Store">The store.</param>
internal sealed record MemoryCreated(MemoryView Store);
