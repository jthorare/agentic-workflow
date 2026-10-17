namespace Aw.Forge.Host.Commands;

/// <summary>Output of a seed command that returns the new thread's key.</summary>
/// <param name="Key">The thread key.</param>
internal sealed record SeedResult(string Key);
