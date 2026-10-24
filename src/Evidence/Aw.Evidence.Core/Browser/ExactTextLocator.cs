namespace Aw.Evidence.Core.Browser;

/// <summary>An element whose text is exactly <paramref name="Text"/>. Several matches are an error unless <paramref name="Nth"/> picks one.</summary>
/// <param name="Text">The exact text.</param>
/// <param name="Nth">Which match, from 1; null needs exactly one.</param>
public sealed record ExactTextLocator(string Text, int? Nth) : Locator;
