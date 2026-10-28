using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the sandbox surfaces (sandbox spec §8). The blockers' sentences are served; these are the words around them, pinned by Golden/Sandbox/states.json.</summary>
internal static partial class Labels
{
    // Health, Settings, park card

    /// <summary>The heading of the Health card and the Settings section.</summary>
    internal const string SandboxHeading = "Sandbox";

    /// <summary>The Health row before the first check.</summary>
    internal const string SandboxRowNeverChecked = "Sandbox: not checked yet";

    /// <summary>The Health row while Sindri doesn't answer.</summary>
    internal const string SandboxRowUnknown = "Sandbox: unknown";

    /// <summary>The Health row when the last check found nothing.</summary>
    internal const string SandboxRowReady = "Sandbox: ready";

    /// <summary>The Settings section while the details can't be read.</summary>
    internal const string SandboxDetailsUnavailable = "Sandbox: details unavailable";

    /// <summary>The Settings section while Sindri doesn't answer.</summary>
    internal const string SandboxStatusUnavailable = "Sandbox: status unavailable";

    /// <summary>The state in a word at the card's right.</summary>
    /// <param name="state">The state.</param>
    /// <param name="waiting">True when the first blocker is a wait or not yet supported (never red).</param>
    /// <returns>"ready", "blocked", "waiting", "learning", "unknown" or "not checked".</returns>
    internal static string SandboxStateWord(ViewModels.Sandbox.SandboxHealthState state, bool waiting) => state switch
    {
        ViewModels.Sandbox.SandboxHealthState.Ready => "ready",
        ViewModels.Sandbox.SandboxHealthState.Blocked => waiting ? "waiting" : "blocked",
        ViewModels.Sandbox.SandboxHealthState.Learning => "learning",
        ViewModels.Sandbox.SandboxHealthState.Unreadable => "details unavailable",
        ViewModels.Sandbox.SandboxHealthState.ServeDown => "unknown",
        ViewModels.Sandbox.SandboxHealthState.NeverChecked => "not checked",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    /// <summary>The Health row while a blocker stands.</summary>
    /// <param name="sentence">The first blocker's sentence.</param>
    /// <returns>"Sandbox: " and the sentence.</returns>
    internal static string SandboxRowBlocked(string sentence) => "Sandbox: " + sentence;

    /// <summary>How long a learn window has left, in days.</summary>
    /// <param name="days">Whole days left, at least 1.</param>
    /// <returns>"1 day" or "6 days".</returns>
    internal static string SandboxDays(int days) => string.Create(CultureInfo.InvariantCulture, $"{days} {(days == 1 ? "day" : "days")}");

    /// <summary>The Health warning row while a learn window is open.</summary>
    /// <param name="repo">The repo, or null for every repo.</param>
    /// <param name="days">Whole days left.</param>
    /// <returns>"Sandbox: learning, network open for api, ends in 6 days".</returns>
    internal static string SandboxRowLearning(string? repo, int days) => "Sandbox: learning, network open for " + (repo ?? "every repo") + ", ends in " + SandboxDays(days);

    /// <summary>The Settings section's line while a learn window is open.</summary>
    /// <param name="days">Whole days left.</param>
    /// <returns>"Sandbox: learning, ends in 6 days".</returns>
    internal static string SandboxSettingsLearning(int days) => "Sandbox: learning, ends in " + SandboxDays(days);

    /// <summary>The Settings field names.</summary>
    internal const string SandboxFieldMechanism = "Mechanism";

    /// <summary>The Settings field name.</summary>
    internal const string SandboxFieldGroups = "Allowed networks";

    /// <summary>The Settings field name.</summary>
    internal const string SandboxFieldLimits = "Limits";

    /// <summary>The Settings field name.</summary>
    internal const string SandboxFieldLearn = "Learn mode";

    /// <summary>The Settings field name.</summary>
    internal const string SandboxFieldLogin = "Sign-in";

    /// <summary>The mechanism in words.</summary>
    /// <param name="mechanism">The served mechanism.</param>
    /// <returns>A short phrase.</returns>
    internal static string SandboxMechanism(string mechanism) => mechanism switch
    {
        "bubblewrap" => "bubblewrap",
        "seatbelt" => "seatbelt",
        _ => "none on this machine",
    };

    /// <summary>The groups, or that none is allowed.</summary>
    internal const string SandboxNoGroups = "none";

    /// <summary>Learn mode when no window is open.</summary>
    internal const string SandboxLearnOff = "off";

    /// <summary>One open learn window.</summary>
    /// <param name="repo">The repo, or null for every repo.</param>
    /// <param name="days">Whole days left.</param>
    /// <returns>"on for api, ends in 6 days".</returns>
    internal static string SandboxLearnOn(string? repo, int days) => "on for " + (repo ?? "every repo") + ", ends in " + SandboxDays(days);

    /// <summary>The limits in a line.</summary>
    /// <param name="memory">The memory ceiling.</param>
    /// <param name="cpu">The CPU quota.</param>
    /// <param name="tasks">The process limit.</param>
    /// <param name="wall">The time limit, or null.</param>
    /// <returns>"12GiB memory, 400% CPU, 512 processes, 40m time".</returns>
    internal static string SandboxLimits(string memory, string cpu, int tasks, string? wall) =>
        string.Create(CultureInfo.InvariantCulture, $"{memory} memory, {cpu} CPU, {tasks} processes") + (wall is { Length: > 0 } ? ", " + wall + " time" : string.Empty);

    // Check again and the park card

    /// <summary>The Check again button, and the live region's resting text.</summary>
    internal const string SandboxCheckAgain = "Check again";

    /// <summary>The Resume button.</summary>
    internal const string SandboxResume = "Resume";

    /// <summary>The Not now button.</summary>
    internal const string SandboxNotNow = "Not now";

    /// <summary>Checked a moment ago.</summary>
    internal const string SandboxCheckedJustNow = "Checked just now";

    /// <summary>The result when nothing blocks and nothing waits.</summary>
    internal const string SandboxReadyNothingWaiting = "Ready. No Steps are waiting.";

    /// <summary>The result when the check could not run.</summary>
    internal const string SandboxCheckFailed = "The check could not run just now.";

    /// <summary>How long ago the last check ran.</summary>
    /// <param name="age">The age.</param>
    /// <returns>"Checked 3m ago" or "Checked just now".</returns>
    internal static string SandboxChecked(TimeSpan age) => age.TotalMinutes < 1 ? SandboxCheckedJustNow : "Checked " + AgoWords(age);

    private static string Steps(int n) => string.Create(CultureInfo.InvariantCulture, $"{n} {(n == 1 ? "Step" : "Steps")}");

    /// <summary>The result of a check that still finds blockers.</summary>
    /// <param name="n">How many.</param>
    /// <returns>"Still blocked: 2 things to fix".</returns>
    internal static string SandboxStillBlocked(int n) => string.Create(CultureInfo.InvariantCulture, $"Still blocked: {n} {(n == 1 ? "thing" : "things")} to fix");

    /// <summary>The list's heading when everything is clear.</summary>
    /// <param name="n">How many Steps would resume.</param>
    /// <returns>"Ready. Resume these 2 Steps?".</returns>
    internal static string SandboxReadyResume(int n) => "Ready. Resume these " + Steps(n) + "?";

    /// <summary>What the live region says when Resume is pressed.</summary>
    /// <param name="n">How many Steps.</param>
    /// <returns>"Ready. Resuming 2 Steps".</returns>
    internal static string SandboxResuming(int n) => "Ready. Resuming " + Steps(n);

    /// <summary>The result when every resume went through.</summary>
    /// <param name="n">How many.</param>
    /// <returns>"Resumed 2 Steps".</returns>
    internal static string SandboxResumed(int n) => "Resumed " + Steps(n);

    /// <summary>The result when some Steps moved on.</summary>
    /// <param name="n">How many resumed.</param>
    /// <param name="m">How many were listed.</param>
    /// <param name="item">The first Step that moved on.</param>
    /// <returns>"Resumed 1 of 2: Fix the login timeout has moved on".</returns>
    internal static string SandboxResumedPartial(int n, int m, string item) => string.Create(CultureInfo.InvariantCulture, $"Resumed {n} of {m}: {item} has moved on");

    /// <summary>What the live region says for a partial resume.</summary>
    /// <param name="n">How many resumed.</param>
    /// <param name="m">How many were listed.</param>
    /// <returns>"Resumed 1 of 2".</returns>
    internal static string SandboxResumedPartialAnnounce(int n, int m) => string.Create(CultureInfo.InvariantCulture, $"Resumed {n} of {m}");

    /// <summary>The park card's name: never the reason code.</summary>
    /// <param name="step">The Step.</param>
    /// <param name="clause">The first clause of the sentence.</param>
    /// <returns>"Needs you: Step x can't start, This machine has no sandbox tool".</returns>
    internal static string SandboxParkName(string step, string clause) => "Needs you: Step " + step + " can't start, " + clause;

    /// <summary>The severity word of a card that is a wait.</summary>
    internal const string SandboxSeverityWaiting = "Waiting";

    /// <summary>The severity word of a card that is not yet supported (gray).</summary>
    internal const string SandboxSeverityNotYet = "Not yet supported";

    /// <summary>The severity word of a card that needs a fix.</summary>
    internal const string SandboxSeverityBlocked = "Blocked";

    /// <summary>The copy button's name for a blocker's command.</summary>
    /// <param name="command">The command.</param>
    /// <returns>"Copy: sudo apt install bubblewrap".</returns>
    internal static string SandboxCopyCommand(string command) => "Copy: " + command;
}
