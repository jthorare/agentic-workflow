namespace Aw.Sindri.Features.ModelCheck;

/// <summary>What the runner said of one check.</summary>
internal enum TlcResult
{
    /// <summary>TLC found no violation.</summary>
    Pass,

    /// <summary>TLC found a broken property, deadlock or assumption.</summary>
    Violation,

    /// <summary>The spec or cfg is wrong, TLC failed, or the line had no result word the step knows.</summary>
    Error,

    /// <summary>The check ran past its time.</summary>
    Timeout,

    /// <summary>A planted bug was caught on the property its cfg names.</summary>
    WitnessCaught,

    /// <summary>A planted bug was not caught.</summary>
    WitnessMissed,

    /// <summary>A planted bug tripped a different property.</summary>
    WitnessWrong,
}
