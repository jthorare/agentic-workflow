namespace Aw.UiDriver.Core.Protocol;

/// <summary>Waits until an element is in a state.</summary>
/// <param name="Selector">The element.</param>
/// <param name="State">'exists', 'visible' (the default), 'hidden', 'gone', 'enabled', 'disabled', 'textEquals' or 'textContains'.</param>
/// <param name="Text">The text for 'textEquals' and 'textContains'.</param>
/// <param name="TimeoutMs">How long to wait; 10000 when null.</param>
internal sealed record WaitForRequest(Selector Selector, string? State = null, string? Text = null, int? TimeoutMs = null)
{
    /// <summary>How long a wait lasts when the request names no time.</summary>
    internal const int DefaultTimeoutMs = 10_000;
}
