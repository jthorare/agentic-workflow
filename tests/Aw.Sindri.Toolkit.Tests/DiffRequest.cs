namespace Aw.Sindri.Toolkit.Tests;

/// <summary>A sample request: two required strings and two optional members.</summary>
public sealed record DiffRequest(string Left, string Right, string? Note, int? Limit);
