namespace Aw.Sindri.Core.Resources;

/// <summary>A named SQLite database: one file per owner and name.</summary>
/// <param name="Name">Lowercase letters, digits and '-'.</param>
public sealed record DatabaseResource(string Name);
