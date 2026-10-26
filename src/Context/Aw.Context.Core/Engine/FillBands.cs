using System.Diagnostics;

namespace Aw.Context.Core.Engine;

/// <summary>
/// The spans of the context fills' bands (issue 1101), named 'Aw.Context' under the default 'Aw.*' listen pattern: 'context.fill.lenses' (the lenses walk), 'context.fill.code.lexical', 'context.fill.code.symbols'
/// and 'context.fill.history'. A span's duration is the band's time, so concurrent fills show which band is slow. Without a listener a span is not created; nothing reaches an answer.
/// </summary>
public static class FillBands
{
    /// <summary>The lenses walk.</summary>
    public const string Lenses = "context.fill.lenses";
    /// <summary>The code fill's lexical search.</summary>
    public const string CodeLexical = "context.fill.code.lexical";
    /// <summary>The code fill's symbol reads.</summary>
    public const string CodeSymbols = "context.fill.code.symbols";
    /// <summary>The history fill.</summary>
    public const string History = "context.fill.history";

    /// <summary>The span source, 'Aw.Context'.</summary>
    public static ActivitySource Source { get; } = new("Aw.Context");

    /// <summary>Starts the span of one band; dispose it when the band is done.</summary>
    public static Activity? Start(string band) => Source.StartActivity(band);
}
