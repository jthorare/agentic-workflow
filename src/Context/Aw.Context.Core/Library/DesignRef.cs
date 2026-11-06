namespace Aw.Context.Core.Library;

/// <summary>A link from an item (or the doc, when <paramref name="From"/> is null) to an item here or in another doc.</summary>
/// <param name="From">The item the link starts at; null when it starts at the doc.</param>
/// <param name="To">An item id in this doc, <c language="text">&lt;doc id&gt;#&lt;item id&gt;</c> in this scope, or <c language="text">doc:&lt;root&gt;[/&lt;slug&gt;]/&lt;doc id&gt;#&lt;item id&gt;</c>.</param>
/// <param name="Kind">How the two ends relate.</param>
/// <param name="Line">The line the link is written on.</param>
public sealed record DesignRef(string? From, string To, DesignRefKind Kind, int Line);
