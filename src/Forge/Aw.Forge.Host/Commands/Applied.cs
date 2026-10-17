namespace Aw.Forge.Host.Commands;

/// <summary>Output of a command that has nothing to return but that it was applied.</summary>
/// <param name="Ok">Always true.</param>
internal sealed record Applied(bool Ok);
