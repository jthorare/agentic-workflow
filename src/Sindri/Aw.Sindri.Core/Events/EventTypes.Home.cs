namespace Aw.Sindri.Core.Events;

/// <summary>The Home event types (home spec §3.1), split out of EventTypes for its file-length limit.</summary>
public static partial class EventTypes
{
    /// <summary>A collector found an object that needs the owner.</summary>
    public const string HomeRowRaised = "home.row.raised";

    /// <summary>A row's blocks, why chip or state changed at the source.</summary>
    public const string HomeRowChanged = "home.row.changed";

    /// <summary>A row's trigger stopped holding, so the row leaves Home.</summary>
    public const string HomeRowCleared = "home.row.cleared";

    /// <summary>The owner saw a row.</summary>
    public const string HomeRowSeen = "home.row.seen";

    /// <summary>The owner snoozed a row.</summary>
    public const string HomeRowSnoozed = "home.row.snoozed";

    /// <summary>A snoozed row woke.</summary>
    public const string HomeRowWoke = "home.row.woke";

    /// <summary>The owner marked a row done.</summary>
    public const string HomeRowDone = "home.row.done";

    /// <summary>A done row came back because its source needs the owner again.</summary>
    public const string HomeRowRevived = "home.row.revived";

    /// <summary>The owner muted a row.</summary>
    public const string HomeRowMuted = "home.row.muted";

    /// <summary>An action on a row went to its source.</summary>
    public const string HomeActSent = "home.act.sent";

    /// <summary>The source accepted an action.</summary>
    public const string HomeActLanded = "home.act.landed";

    /// <summary>The source refused an action or did not answer.</summary>
    public const string HomeActFailed = "home.act.failed";

    /// <summary>The brief job wrote 'since you last looked'.</summary>
    public const string HomeBriefWritten = "home.brief.written";

    /// <summary>The owner looked at Home, which moves the 'since you last looked' mark.</summary>
    public const string HomeCursorMoved = "home.cursor.moved";
}
