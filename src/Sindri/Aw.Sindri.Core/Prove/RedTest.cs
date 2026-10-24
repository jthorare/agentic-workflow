namespace Aw.Sindri.Core.Prove;

/// <summary>One red test.</summary>
/// <param name="Name">The test name or id as the builder wrote it.</param>
/// <param name="Path">The file it is in; null for the short form.</param>
public sealed record RedTest(string Name, string? Path);
