namespace Aw.Context.Core.ConceptScan;

/// <summary>How a scan ended. None of these is a failure: a scan that finds nothing leaves the map as it was.</summary>
public enum ConceptScanOutcome
{
    /// <summary>The hints were stored on the draft.</summary>
    Scanned,

    /// <summary>The draft has no feature to group.</summary>
    NothingToScan,

    /// <summary>No agent CLI answered one of the two passes.</summary>
    ModelUnavailable,

    /// <summary>The model answered, but no concept survived the checks.</summary>
    NoConcepts,
}
