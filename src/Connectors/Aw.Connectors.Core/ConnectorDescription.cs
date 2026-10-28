namespace Aw.Connectors.Core;

/// <summary>What a connector says about itself.</summary>
/// <param name="Source">The record source word: 'github', 'slack'.</param>
/// <param name="Name">A name a person reads.</param>
public sealed record ConnectorDescription(string Source, string Name);
