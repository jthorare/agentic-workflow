namespace Aw.Context.Core.Linear;

/// <summary>The profile names a write key that can't be used.</summary>
/// <param name="Reason">Why, in words the owner can act on.</param>
public sealed record LinearWriteKeyRefused(string Reason) : LinearWriteKeyStatus;
