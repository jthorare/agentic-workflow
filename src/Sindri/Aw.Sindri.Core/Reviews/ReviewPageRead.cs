namespace Aw.Sindri.Core.Reviews;

/// <summary>One forge read of the page: what it gave, or why it failed. The reason is forge text and untrusted.</summary>
/// <typeparam name="T">What the read gives.</typeparam>
/// <param name="Value">The value; its default when the read failed.</param>
/// <param name="Error">Why the read failed, in words; null when it worked.</param>
public sealed record ReviewPageRead<T>(T Value, string? Error = null)
{
    /// <summary>Whether the read worked.</summary>
    public bool Ok => Error is null;
}
