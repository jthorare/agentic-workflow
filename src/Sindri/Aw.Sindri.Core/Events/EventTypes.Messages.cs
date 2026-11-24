namespace Aw.Sindri.Core.Events;

/// <summary>The Messages event types (messages spec §2 decision 2): ids and counts only, never message text.</summary>
public static partial class EventTypes
{
    /// <summary>The messages-sync service read Slack: counts per path and state, at most once per 15 minutes or on a change of path or state (M1e writes it).</summary>
    public const string MessagesSynced = "messages.synced";
}
