using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Sync;

/// <summary>One page of a feed (the shape of 'ConnectorPage', spec §5.2).</summary>
/// <param name="Items">Records changed or new.</param>
/// <param name="Removed">Records the source no longer returns.</param>
/// <param name="Next">The cursor after this page; the driver persists it with the page's records.</param>
/// <param name="CursorReset">The source did not recognise the cursor and started over.</param>
public sealed record RecordPage(IReadOnlyList<RecordItem> Items, IReadOnlyList<RecordKey> Removed, SourceCursor Next, bool CursorReset);
