namespace Aw.Connectors.Core;

/// <summary>A reference from one record to another, in the record key scheme.</summary>
/// <param name="Kind">How they relate: 'closes' or 'relates'.</param>
/// <param name="Key">The record key of the target, such as 'github:issue/acme/widgets/7'.</param>
public sealed record ConnectorLink(string Kind, string Key);
