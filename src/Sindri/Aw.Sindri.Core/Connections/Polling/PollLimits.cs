namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>Limits every poller shares.</summary>
public static class PollLimits
{
    /// <summary>The most pages one poll reads before it reports a gap.</summary>
    public const int DefaultPageCap = 10;
}
