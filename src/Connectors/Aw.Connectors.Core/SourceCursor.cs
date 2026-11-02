namespace Aw.Connectors.Core;

/// <summary>Where a connector's read stands, opaque to everyone but the connector that made it (the Host stores it per source and hands it back).</summary>
/// <param name="Value">The connector's own text form.</param>
public sealed record SourceCursor(string Value);
