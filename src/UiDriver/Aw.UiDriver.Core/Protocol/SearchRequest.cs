namespace Aw.UiDriver.Core.Protocol;

/// <summary>Finds the elements a selector matches.</summary>
/// <param name="Selector">What to find.</param>
/// <param name="Limit">The most matches to report; 50 when null.</param>
internal sealed record SearchRequest(Selector Selector, int? Limit = null)
{
    /// <summary>The most matches reported when a search names no limit.</summary>
    internal const int DefaultLimit = 50;
}
