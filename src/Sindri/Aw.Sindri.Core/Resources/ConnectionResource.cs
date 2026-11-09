namespace Aw.Sindri.Core.Resources;

/// <summary>A connection the owner reads by name.</summary>
/// <param name="Name">The connection's name, such as 'github'. The directory resolves it.</param>
public sealed record ConnectionResource(string Name);
