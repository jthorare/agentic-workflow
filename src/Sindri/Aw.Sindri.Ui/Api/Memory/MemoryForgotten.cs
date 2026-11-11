namespace Aw.Sindri.Ui.Api.Memory;

/// <summary>DELETE /sindri/memories/{name}/facts.</summary>
/// <param name="Deleted">How many facts were deleted.</param>
internal sealed record MemoryForgotten(int Deleted);
