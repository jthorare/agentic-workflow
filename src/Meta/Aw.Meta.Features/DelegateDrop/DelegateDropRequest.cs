namespace Aw.Meta.Features.DelegateDrop;

/// <summary>The DelegateDrop request: forget the delegation of one issue, owed or settled.</summary>
/// <param name="Issue">The issue number.</param>
public sealed record DelegateDropRequest(int Issue);
