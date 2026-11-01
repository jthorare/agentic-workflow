namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The values of <see cref="MapRebuildView.Phase"/>.</summary>
#pragma warning disable MA0182 // Q7b: the pin cards read it in the next commits
internal static class MapRebuildPhases
{
    /// <summary>No refresh runs and nothing blocks one.</summary>
    internal const string Idle = "idle";

    /// <summary>A refresh is queued or running.</summary>
    internal const string Building = "building";

    /// <summary>A built map is held for review.</summary>
    internal const string MapHeld = "mapHeld";

    /// <summary>No mapped repo is on its main branch.</summary>
    internal const string NoMainBranch = "noMainBranch";

    /// <summary>The backoff after failed refreshes.</summary>
    internal const string Backoff = "backoff";
}
#pragma warning restore MA0182
