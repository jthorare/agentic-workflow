using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Library;

/// <summary>One stored item of a design doc, read through its doc's audience.</summary>
/// <param name="DocKey">The doc record.</param>
/// <param name="Key">The item id.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Parent">The item it belongs to; null at the top.</param>
/// <param name="LineStart">Its first line.</param>
/// <param name="LineEnd">Its last line, inclusive.</param>
/// <param name="Text">Its text, capped at <see cref="DesignDocParser.MaxTextChars"/> characters.</param>
public sealed record LibraryItem(RecordKey DocKey, string Key, DesignItemKind Kind, string? Parent, int LineStart, int LineEnd, string Text);
