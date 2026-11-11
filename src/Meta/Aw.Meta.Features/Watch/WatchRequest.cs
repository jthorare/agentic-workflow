namespace Aw.Meta.Features.Watch;

/// <summary>The Watch request: poll until the duration is over, emitting a line per settled change.</summary>
/// <param name="Interval">The time between polls.</param>
/// <param name="Duration">How long this watch runs before it asks to be re-armed.</param>
/// <param name="Emit">Receives each line as it happens (the CLI prints and flushes it).</param>
public sealed record WatchRequest(TimeSpan Interval, TimeSpan Duration, Func<string, ValueTask> Emit)
{
    /// <summary>End the watch at the first poll where the meta's inbox holds messages still owed a reply (a Codex or Cursor meta polls in the foreground).</summary>
    public bool UntilMail { get; init; }

    /// <summary>No window and no WATCH EXPIRING: keep polling until a poll prints a real line (anything but the start line), print it and end (a Claude meta runs this as a background command that wakes the model on exit; #2498).</summary>
    public bool UntilEvent { get; init; }

    /// <summary>Report every pull request in the repo, not only the team's (#2771).</summary>
    public bool AllPrs { get; init; }

    /// <summary>This watch's pid (#2564): the newest watch writes it to watch.owner, and only the owner keeps the saved baseline.</summary>
    public int Owner { get; init; }
}
