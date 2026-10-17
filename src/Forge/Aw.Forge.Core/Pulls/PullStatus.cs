namespace Aw.Forge.Core.Pulls;

/// <summary>Where a pull request is in its life.</summary>
public enum PullStatus
{
    /// <summary>Open.</summary>
    Open = 1,

    /// <summary>Closed without merging.</summary>
    Closed = 2,

    /// <summary>Merged.</summary>
    Merged = 3,
}
