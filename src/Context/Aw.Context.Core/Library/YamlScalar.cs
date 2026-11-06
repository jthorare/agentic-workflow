namespace Aw.Context.Core.Library;

/// <summary>One front-matter value as written: its text without quotes, and whether it was quoted (a quoted <c language="text">412</c> is not an issue number).</summary>
/// <param name="Value">The text.</param>
/// <param name="Quoted">True when it was in quotes.</param>
internal sealed record YamlScalar(string Value, bool Quoted);
