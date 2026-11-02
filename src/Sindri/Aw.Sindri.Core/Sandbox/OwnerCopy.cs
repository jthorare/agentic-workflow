namespace Aw.Sindri.Core.Sandbox;

/// <summary>One owner sentence.</summary>
/// <param name="Text">The sentence, in the Step wording.</param>
/// <param name="Command">The copyable second line, or null.</param>
/// <param name="State">'gray' (not yet supported, never red), 'wait' (a waiting state, never red), or null.</param>
public sealed record OwnerCopy(string Text, string? Command, string? State);
