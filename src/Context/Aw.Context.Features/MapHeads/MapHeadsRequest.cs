using Aw.Context.Core.Engine;

namespace Aw.Context.Features.MapHeads;

/// <summary>Reads where each mapped repo's checkout stands: its HEAD and whether the default branch holds it.</summary>
/// <param name="Scope">The scope whose mapped repos are read.</param>
internal sealed record MapHeadsRequest(ContextScope Scope);
