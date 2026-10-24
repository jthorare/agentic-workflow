namespace Aw.Judge.Features.CheckTraits;

/// <summary>What 'judge trait-check' judges.</summary>
public enum TraitCheckMode
{
    /// <summary>'--edit --file &lt;p&gt;…': the diff of the files just edited (the PostToolUse hook).</summary>
    Edit = 1,

    /// <summary>'--stop [--base ref]': the branch's diff against 'origin/main' (the Stop hook).</summary>
    Stop = 2,

    /// <summary>'--scan [--path p…]': whole files, by hand.</summary>
    Scan = 3,
}
