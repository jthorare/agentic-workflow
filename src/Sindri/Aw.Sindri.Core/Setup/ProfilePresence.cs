namespace Aw.Sindri.Core.Setup;

/// <summary>Whether there is a profile file (edits apply on save, #2258 PR 1: no approval states).</summary>
public enum ProfilePresence
{
    /// <summary>There is no profile file.</summary>
    Missing,

    /// <summary>The profile file is there; it is live.</summary>
    Present,
}
