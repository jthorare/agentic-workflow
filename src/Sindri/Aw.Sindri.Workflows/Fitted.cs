namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>A text after <see cref="StoredJson.Fit"/>.</summary>
/// <param name="Text">The text that fits, or null.</param>
/// <param name="Cut">True when Text is the envelope for a text that was over the cap.</param>
internal readonly record struct Fitted(string? Text, bool Cut);
