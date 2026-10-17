namespace Aw.Forge.Core.Pulls;

/// <summary>How a push ended, when git itself worked.</summary>
public enum PushOutcome
{
    /// <summary>The branch now points at the sha.</summary>
    Pushed = 1,

    /// <summary>The lease failed: the branch moved since the caller looked. Nothing changed.</summary>
    HeadMoved = 2,
}
