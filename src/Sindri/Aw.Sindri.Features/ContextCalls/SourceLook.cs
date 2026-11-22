namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Whether a context source needs a look, and the words that say why when the row's own state words don't (#3832).</summary>
/// <param name="NeedsLook">True when the source needs a look.</param>
/// <param name="Words">The reason the Health page shows, or null when the row's state says it (or there is no look).</param>
internal readonly record struct SourceLook(bool NeedsLook, string? Words);
