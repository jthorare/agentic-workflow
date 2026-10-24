namespace Aw.Judge.Core.Traits;

/// <summary>What a broken trait does.</summary>
public enum TraitSeverity
{
    /// <summary>The Stop gate holds the agent back until it is fixed or explained.</summary>
    Block = 1,

    /// <summary>Reported on an edit and in a scan only.</summary>
    Warn = 2,
}
