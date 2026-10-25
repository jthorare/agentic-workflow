namespace Aw.UiDriver.Core.Protocol;

/// <summary>The element tree below an element (the window when no selector is given).</summary>
/// <param name="From">Where to start; the first window when null.</param>
/// <param name="Depth">How many levels below it to report; 6 when null.</param>
internal sealed record TreeRequest(Selector? From = null, int? Depth = null)
{
    /// <summary>The depth of a tree that asks for none.</summary>
    internal const int DefaultDepth = 6;

    /// <summary>The most levels a tree answer carries; a deeper ask is read as this.</summary>
    internal const int MaxDepth = 64;
}
