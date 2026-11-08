namespace Aw.Sindri.Features.ForgetMemoryFact;

/// <summary>How many facts were forgotten.</summary>
/// <param name="Deleted">The count (0 when none matched).</param>
public sealed record ForgetMemoryFactResponse(int Deleted);
