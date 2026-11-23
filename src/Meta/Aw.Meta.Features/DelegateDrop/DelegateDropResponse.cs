namespace Aw.Meta.Features.DelegateDrop;

/// <summary>The DelegateDrop response.</summary>
/// <param name="Issue">The issue number.</param>
/// <param name="Dropped">Whether a delegation of the issue was recorded and is now removed; false when none was.</param>
/// <param name="Lane">The lane the dropped delegation named; null when nothing was dropped.</param>
public sealed record DelegateDropResponse(int Issue, bool Dropped, string? Lane);
