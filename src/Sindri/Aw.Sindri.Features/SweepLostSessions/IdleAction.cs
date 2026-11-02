namespace Aw.Sindri.Features.SweepLostSessions;

/// <summary>What the idle sweep does with one Step session.</summary>
public enum IdleAction
{
    /// <summary>Leave it alone.</summary>
    None,

    /// <summary>Send it the one re-prompt.</summary>
    Nudge,

    /// <summary>End it as idle.</summary>
    End,
}
