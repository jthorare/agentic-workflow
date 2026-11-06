namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The finding a fix task is about (review page spec §8.2): a failed check, an unclaimed requirement or a diff line.</summary>
/// <param name="Kind">Where it comes from.</param>
/// <param name="Title">The check's or requirement's title; the line's text for a line.</param>
/// <param name="SourceId">The check or requirement id; empty for a line.</param>
/// <param name="Detail">The check's reason; empty for a requirement or a line.</param>
/// <param name="Path">The file of a line; empty otherwise.</param>
/// <param name="Line">The line number of a line; null otherwise.</param>
/// <param name="Added">True when the line is an added one ("added line" chip).</param>
internal sealed record ReviewFixSource(ReviewFixKind Kind, string Title, string SourceId, string Detail, string Path, int? Line, bool Added);
