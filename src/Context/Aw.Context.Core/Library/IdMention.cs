namespace Aw.Context.Core.Library;

/// <summary>One id written in a cell or a key line: the item, and the doc prefix when the writer gave one.</summary>
/// <param name="Key">The id (a phase without leading zeros).</param>
/// <param name="Kind">The id's kind.</param>
/// <param name="To">The text a ref stores: <c language="text">id</c>, or <c language="text">doc#id</c> when written with a doc.</param>
internal readonly record struct IdMention(string Key, DesignItemKind Kind, string To);
