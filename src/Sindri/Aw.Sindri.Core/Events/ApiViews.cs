namespace Aw.Sindri.Core.Events;

/// <summary>
/// The view names an event's 'invalidates' list uses (UI spec §6.7 E-15): the API resources the app re-queries when the
/// event arrives, not views.db tables. P4 and P6 add theirs.
/// </summary>
public static class ApiViews
{
    /// <summary>GET /sindri/status.</summary>
    public const string Status = "status";

    /// <summary>GET /sindri/services.</summary>
    public const string Services = "services";

    /// <summary>GET /sindri/profile.</summary>
    public const string Profile = "profile";

    /// <summary>GET /sindri/commands.</summary>
    public const string Commands = "commands";

    /// <summary>GET /sindri/queue.</summary>
    public const string Queue = "queue";

    /// <summary>GET /sindri/items.</summary>
    public const string Items = "items";

    /// <summary>GET /sindri/items/{itemId}.</summary>
    public const string Item = "item";

    /// <summary>GET /sindri/items/{itemId}/timeline.</summary>
    public const string Timeline = "timeline";

    /// <summary>GET /sindri/needs-you.</summary>
    public const string NeedsYou = "needs-you";

    /// <summary>GET /sindri/requests.</summary>
    public const string Requests = "requests";

    /// <summary>GET /sindri/decisions.</summary>
    public const string Decisions = "decisions";

    /// <summary>GET /sindri/jobs/{jobId}.</summary>
    public const string Job = "job";

    /// <summary>GET /sindri/jobs.</summary>
    public const string Jobs = "jobs";

    /// <summary>GET /sindri/launches/{launchId}.</summary>
    public const string Launch = "launch";

    /// <summary>GET /sindri/launch/targets.</summary>
    public const string LaunchTargets = "launch-targets";

    /// <summary>Every view name.</summary>
    public static IReadOnlyList<string> All { get; } = [Status, Services, Profile, Commands, Queue, Items, Item, Timeline, NeedsYou, Requests, Decisions, Job, Jobs, Launch, LaunchTargets];
}
