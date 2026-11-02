namespace Aw.Connectors.Core;

/// <summary>What one pull of a connector read.</summary>
/// <param name="Items">The records changed since the cursor.</param>
/// <param name="Removed">Keys the source no longer returns.</param>
/// <param name="Next">Where the next pull starts.</param>
/// <param name="CursorReset">True when the cursor could not be honoured and the pull started over.</param>
public sealed record ConnectorPage(IReadOnlyList<ConnectorItem> Items, IReadOnlyList<string> Removed, SourceCursor Next, bool CursorReset);
