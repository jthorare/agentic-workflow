namespace Aw.Sindri.Features.Line;

/// <summary>Where a phase of the line is (line spec §8.2): the board's state of its row.</summary>
internal enum PhaseState
{
    /// <summary>Not started: a first attempt to come, or a retry the owner asked for.</summary>
    Waiting,

    /// <summary>Building to shipping; a parked phase is a running one with its park reason.</summary>
    Running,

    /// <summary>Its PR is open, babysat, and waiting on the owner's merge.</summary>
    Shipped,

    /// <summary>Merged.</summary>
    Merged,

    /// <summary>Ended without merging; holds the line for the owner.</summary>
    Failed,

    /// <summary>The owner skipped it, or a phase it depends on.</summary>
    Skipped,
}
