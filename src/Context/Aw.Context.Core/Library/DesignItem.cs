using System.Text.Json.Serialization;

namespace Aw.Context.Core.Library;

/// <summary>One item of a design doc: a section, phase, requirement, acceptance criterion, test-plan row or task.</summary>
/// <param name="Key">The item id (a section's is its anchor; a phase is stored without leading zeros).</param>
/// <param name="Kind">What it is.</param>
/// <param name="Parent">The item it belongs to: an acceptance criterion's requirement, or the section it sits under; null at the top.</param>
/// <param name="LineStart">Its first line (from 1).</param>
/// <param name="LineEnd">Its last line, inclusive.</param>
/// <param name="Text">Its line and the lines under it, capped at <see cref="DesignDocParser.MaxTextChars"/> characters.</param>
public sealed record DesignItem([property: JsonPropertyName("id")] string Key, DesignItemKind Kind, string? Parent, int LineStart, int LineEnd, string Text);
