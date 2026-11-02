using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the sandbox on the Agents sheet, the item sheet's Blocked list, the waiting state, the limit ending and the takeover banner (SB-6b; golden/Sandbox/copy.json, states.json).</summary>
internal static partial class Labels
{
    private const double Gib = 1024d * 1024d * 1024d;

    /// <summary>The sandbox line on a session's sheet: "Sandbox: on, writes only to /work/api".</summary>
    /// <param name="folder">The writable folder; null when none is served.</param>
    /// <param name="attended">True for a session the owner opened.</param>
    /// <returns>The line.</returns>
    internal static string SandboxLine(string? folder, bool attended)
    {
        var on = attended ? "Sandbox: on, attended" : "Sandbox: on";
        return folder is { Length: > 0 } ? on + ", writes only to " + folder : on;
    }

    /// <summary>The headroom as text: "memory 2.1 of 12 GiB".</summary>
    /// <param name="usedBytes">The memory used.</param>
    /// <param name="maxBytes">The ceiling.</param>
    /// <returns>The text.</returns>
    internal static string SandboxHeadroom(long usedBytes, long maxBytes) =>
        string.Create(CultureInfo.InvariantCulture, $"memory {usedBytes / Gib:0.0} of {maxBytes / Gib:0.#} GiB");

    /// <summary>The Blocked list's heading.</summary>
    internal const string BlockedHeading = "Blocked";

    /// <summary>The list when nothing was blocked.</summary>
    internal const string BlockedNothing = "Nothing was blocked";

    /// <summary>The "See all blocked" link under a long list.</summary>
    internal const string SeeAllBlocked = "See all blocked";

    /// <summary>One target in the short list: "Blocked: host0.example.org × 10".</summary>
    /// <param name="target">The host, path or tool.</param>
    /// <param name="count">How many times.</param>
    /// <returns>The line.</returns>
    internal static string BlockedSome(string target, int count) => string.Create(CultureInfo.InvariantCulture, $"Blocked: {target} × {count}");

    /// <summary>The head of a long list: "Blocked: top 5 of 12".</summary>
    /// <param name="total">How many targets in all.</param>
    /// <returns>The line.</returns>
    internal static string BlockedMany(int total) => string.Create(CultureInfo.InvariantCulture, $"Blocked: top 5 of {total}");

    /// <summary>A host row.</summary>
    /// <param name="target">The host.</param>
    /// <param name="count">How many times.</param>
    /// <returns>The sentence.</returns>
    internal static string BlockedHost(string target, int count) => string.Create(CultureInfo.InvariantCulture, $"Blocked host {target} {count} times; allow it for this repo");

    /// <summary>A path row.</summary>
    /// <param name="target">The path.</param>
    /// <param name="count">How many times.</param>
    /// <returns>The sentence.</returns>
    internal static string BlockedPath(string target, int count) => string.Create(CultureInfo.InvariantCulture, $"Blocked path {target} {count} times; allow it for this repo");

    /// <summary>A path that is never shared.</summary>
    /// <param name="target">The path.</param>
    /// <param name="reason">Why, as served.</param>
    /// <returns>The sentence.</returns>
    internal static string BlockedPathPermanent(string target, string reason) => $"Blocked path {target}: never allowed, {reason}";

    /// <summary>A host that is never allowed (a private address, or one over the hit cap).</summary>
    /// <param name="target">The host.</param>
    /// <param name="reason">Why, as served.</param>
    /// <returns>The sentence.</returns>
    internal static string BlockedHostPermanent(string target, string reason) => $"Blocked host {target}: never allowed, {reason}";

    /// <summary>A tool the owner must run themselves.</summary>
    /// <param name="name">The tool.</param>
    /// <param name="count">How many times.</param>
    /// <returns>The sentence.</returns>
    internal static string BlockedPolicy(string name, int count) => string.Create(CultureInfo.InvariantCulture, $"Blocked tool {name} {count} times; push from your own terminal");

    /// <summary>The Allow button.</summary>
    internal const string AllowButton = "Allow";

    /// <summary>The Allow button's accessible name: "Allow host0.example.org".</summary>
    /// <param name="target">The target.</param>
    /// <returns>The name.</returns>
    internal static string AllowName(string target) => AllowButton + " " + target;

    /// <summary>What the live region says while Allow is on its way.</summary>
    internal const string AllowSending = "Sending";

    /// <summary>What the live region says once the approval is open.</summary>
    /// <param name="target">The target.</param>
    /// <returns>The sentence.</returns>
    internal static string AllowNeedsApproval(string target) => "Needs your approval: open the approval to allow " + target;

    /// <summary>Allow went through.</summary>
    internal const string AllowDone = "Allowed";

    /// <summary>Allow was refused.</summary>
    internal const string AllowRefused = "Refused";

    /// <summary>The live region's resting text.</summary>
    internal const string AllowResting = "Blocked list";

    /// <summary>A run waiting on the session cap.</summary>
    /// <param name="running">Sessions running.</param>
    /// <param name="cap">The cap.</param>
    /// <returns>The line.</returns>
    internal static string WaitingForSlot(int running, int cap) => string.Create(CultureInfo.InvariantCulture, $"Waiting for a free session slot ({running} of {cap} running)");

    /// <summary>What the live region says once.</summary>
    /// <param name="running">Sessions running.</param>
    /// <param name="cap">The cap.</param>
    /// <returns>The sentence.</returns>
    internal static string WaitingForSlotAnnounce(int running, int cap) => string.Create(CultureInfo.InvariantCulture, $"Waiting for a free session slot, {running} of {cap} running");

    /// <summary>The wait is over.</summary>
    internal const string SlotStarted = "Started";

    /// <summary>The waiting region's resting text.</summary>
    internal const string WaitingResting = "Session slot";

    /// <summary>A limit ending.</summary>
    /// <param name="kind">'memory' or 'wall'.</param>
    /// <param name="limit">The limit as served ('12GiB').</param>
    /// <param name="elapsedSeconds">The seconds it ran, as served.</param>
    /// <param name="branch">The kept branch; null when none.</param>
    /// <returns>The sentence.</returns>
    internal static string LimitEnding(string kind, string limit, string elapsedSeconds, string? branch)
    {
        var time = SandboxTime(long.TryParse(elapsedSeconds, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ? seconds : 0);
        var head = string.Equals(kind, "memory", StringComparison.Ordinal)
            ? $"This Step ran out of memory ({limit}) after {time}."
            : $"This Step ran out of time after {time}.";
        return branch is { Length: > 0 } ? head + $" Its work so far is kept on branch {branch}." : head;
    }

    /// <summary>A run time in words: "45 s", "12 min", "1 h 5 min".</summary>
    /// <param name="seconds">The seconds.</param>
    /// <returns>The time.</returns>
    internal static string SandboxTime(long seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (span.TotalMinutes < 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalSeconds} s");
        }

        var hours = (int)span.TotalHours;
        return hours == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{span.Minutes} min")
            : span.Minutes == 0 ? string.Create(CultureInfo.InvariantCulture, $"{hours} h") : string.Create(CultureInfo.InvariantCulture, $"{hours} h {span.Minutes} min");
    }

    /// <summary>The takeover banner (sandbox spec §7).</summary>
    internal const string SandboxTakeoverBanner = "Sandboxed session. Pushes and secrets are blocked. The clock is paused while you're here. To push or read a secret, open a new window: it is outside the sandbox. `sindri sandbox exec` shows what the agent saw.";

    /// <summary>The banner's dismiss button.</summary>
    internal const string DismissBanner = "Dismiss";

    /// <summary>The banner's live region resting text.</summary>
    internal const string BannerResting = "Takeover";
}
