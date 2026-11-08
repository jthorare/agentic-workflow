using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Features.CreateMemory;

/// <summary>The memory that was made.</summary>
/// <param name="Store">The view.</param>
public sealed record CreateMemoryResponse(MemoryView Store);
