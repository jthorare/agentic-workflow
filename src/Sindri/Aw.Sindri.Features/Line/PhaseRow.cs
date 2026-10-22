using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

/// <summary>One phase of the line's board (<see cref="PhaseBoard"/>).</summary>
internal sealed class PhaseRow
{
    /// <summary>The phase's id.</summary>
    internal string Phase { get; init; } = string.Empty;

    /// <summary>The frozen packet entry <c language="text">{ phase, id, hash, dir }</c>.</summary>
    internal JsonObject Entry { get; init; } = [];

    /// <summary>The ids of the phases it depends on.</summary>
    internal List<string> Deps { get; init; } = [];

    /// <summary>Where the phase is.</summary>
    internal PhaseState State { get; set; }

    /// <summary>The attempt that started last; 0 before the first.</summary>
    internal int Attempt { get; set; }

    /// <summary>The head branch of its PR, once shipped.</summary>
    internal string? HeadBranch { get; set; }

    /// <summary>Whether the phase is parked.</summary>
    internal bool Parked { get; set; }

    /// <summary>Why it is parked.</summary>
    internal string? ParkReason { get; set; }

    /// <summary>The hash of the phase packet in force (the newest one frozen under the phase's id, amendments included), once a run ended with it.</summary>
    internal string? Packet { get; set; }

    /// <summary>Whether the phase is over: merged or skipped.</summary>
    internal bool Settled => State is PhaseState.Merged or PhaseState.Skipped;

    /// <summary>The state's name on the board.</summary>
    /// <param name="state">The state.</param>
    /// <returns>Its lower-case name.</returns>
    internal static string Name(PhaseState state) => state switch
    {
        PhaseState.Running => "running",
        PhaseState.Shipped => "shipped",
        PhaseState.Merged => "merged",
        PhaseState.Failed => "failed",
        PhaseState.Skipped => "skipped",
        PhaseState.Waiting or _ => "waiting",
    };

    /// <summary>The state a board names; <see cref="PhaseState.Waiting"/> for a name it does not know.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The state.</returns>
    internal static PhaseState StateOf(string? name) => name switch
    {
        "running" => PhaseState.Running,
        "shipped" => PhaseState.Shipped,
        "merged" => PhaseState.Merged,
        "failed" => PhaseState.Failed,
        "skipped" => PhaseState.Skipped,
        _ => PhaseState.Waiting,
    };

    /// <summary>The row as the board stores it.</summary>
    /// <returns>The JSON.</returns>
    internal JsonObject ToJson()
    {
        var row = new JsonObject
        {
            ["phase"] = Phase,
            ["entry"] = Entry.DeepClone(),
            ["deps"] = LineJson.ToArray(Deps),
            ["state"] = Name(State),
            ["attempt"] = Attempt,
            ["parked"] = Parked,
        };
        if (HeadBranch is not null)
        {
            row["headBranch"] = HeadBranch;
        }

        if (ParkReason is not null)
        {
            row["parkReason"] = ParkReason;
        }

        if (Packet is not null)
        {
            row["packet"] = Packet;
        }

        return row;
    }
}
