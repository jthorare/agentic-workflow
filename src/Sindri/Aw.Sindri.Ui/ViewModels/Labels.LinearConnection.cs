using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): Settings → Connections → Linear, the two keys and the teams (Linear hub spec §3, §5.1, §8).</summary>
internal static partial class Labels
{
    /// <summary>The group's heading.</summary>
    internal const string ConnectionsHeading = "Connections";

    /// <summary>The line under the group's heading.</summary>
    internal const string ConnectionsIntro = "Outside systems Sindri reads from and, where you allow it, writes to.";

    /// <summary>The Linear card's name.</summary>
    internal const string LinearConnName = "Linear";

    /// <summary>The card's summary before a read key is stored.</summary>
    internal const string LinearConnNotConnected = "Not connected";

    /// <summary>The small heading over the read key's part of the card.</summary>
    internal const string LinearConnReadHeading = "Read key";

    /// <summary>The small heading over the write key's part of the card.</summary>
    internal const string LinearConnWriteHeading = "Write key";

    /// <summary>The line over the empty read key box.</summary>
    internal const string LinearConnReadIntro = "Paste a key to connect Linear. A read-only one is enough to list your issues.";

    /// <summary>The line under the read key box: where the key is made and where it goes.</summary>
    internal const string LinearConnReadWhere = "Linear → Settings → Account → Security and access → Personal API keys. The key goes to your keychain, never a file or the log.";

    /// <summary>The line over the empty write key box.</summary>
    internal const string LinearConnWriteIntro = "Paste a write key to edit from Sindri. It must be a different key from the read one, made with write permission.";

    /// <summary>The read key box's accessible name.</summary>
    internal const string LinearConnReadKeyName = "Linear read key";

    /// <summary>The write key box's accessible name.</summary>
    internal const string LinearConnWriteKeyName = "Linear write key";

    /// <summary>The key boxes' placeholder.</summary>
    internal const string LinearConnKeyPlaceholder = "lin_api_…";

    /// <summary>The button that reads the teams a pasted key can see.</summary>
    internal const string LinearConnListTeams = "List my teams";

    /// <summary>The legend over the team boxes.</summary>
    internal const string LinearConnTeamsLegend = "Teams Sindri may write to";

    /// <summary>The button that stores a key.</summary>
    internal const string LinearConnCheckAndSave = "Check and save";

    /// <summary>Under the team boxes with none ticked.</summary>
    internal const string LinearConnTickOne = "Sindri only writes to teams you picked. Tick at least one.";

    /// <summary>Under the team boxes with at least one ticked.</summary>
    internal const string LinearConnSaveHint = "Stores the key, sets your teams and approves just this change.";

    /// <summary>While the teams are being read.</summary>
    internal const string LinearConnLookingUp = "Looking up your teams…";

    /// <summary>The button that puts a stored key back in the paste box.</summary>
    internal const string LinearConnReplace = "Replace key";

    /// <summary>The button that asks to remove the write key.</summary>
    internal const string LinearConnRemove = "Remove key";

    /// <summary>The removal question's accessible name.</summary>
    internal const string LinearConnRemoveName = "Remove the write key?";

    /// <summary>The removal question.</summary>
    internal const string LinearConnRemoveAsk = "Remove the write key? Sindri deletes it from your keychain and clears writeToken. Edits from Sindri stop; reading keeps working.";

    /// <summary>The removal question's no.</summary>
    internal const string LinearConnKeep = "Keep it";

    /// <summary>The removal question's yes.</summary>
    internal const string LinearConnRemoveIt = "Remove";

    /// <summary>The button beside the waiting line: the screen that approves the profile's changes together.</summary>
    internal const string LinearConnReview = "Review changes";

    /// <summary>After a save or a removal the profile's other changes hold back.</summary>
    internal const string LinearConnWaiting = "Saved. Other profile changes are waiting, so this one wasn't approved on its own. Review them together.";

    /// <summary>Read from the API with no save in this card: the profile's Linear keys differ from the approved ones.</summary>
    internal const string LinearConnPending = "Your Linear change waits for approval.";

    /// <summary>A stored write key whose scope Linear does not say, on a later open (not the click's own answer).</summary>
    internal const string LinearConnScopeUnknownRead = "Linear doesn't say if this key can write. Your first Save will tell.";

    /// <summary>The button that leaves a Replace key box with nothing changed.</summary>
    internal const string LinearConnCancel = "Cancel";

    /// <summary>After a read key was saved while other profile changes wait.</summary>
    internal const string LinearConnWaitingRead = "Saved the key and teams. Other profile changes are waiting, so this one wasn't approved on its own. Review them together.";

    /// <summary>After a write key was saved and Linear does not say what it can do.</summary>
    internal const string LinearConnScopeUnknown = "Saved. Linear doesn't say if this key can write. Your first Save will tell.";

    /// <summary>SND-LINEAR-004 on the read key.</summary>
    internal const string LinearConnReadRefused = "Linear refused the read key. Check it and paste it again. Nothing was stored.";

    /// <summary>SND-LINEAR-004 on the write key.</summary>
    internal const string LinearConnWriteRefused = "Linear refused the write key. Make a new one with write permission. Nothing was stored.";

    /// <summary>A stored read key that Linear now refuses.</summary>
    internal const string LinearConnReadStale = "Linear refused the stored read key. Paste a new one.";

    /// <summary>A stored write key that Linear now refuses.</summary>
    internal const string LinearConnWriteStale = "Linear refused the stored write key. Paste a new one.";

    /// <summary>While a read key is stored.</summary>
    internal const string LinearConnChecking = "Checking…";

    /// <summary>While the write key is removed.</summary>
    internal const string LinearConnRemoving = "Removing the write key…";

    /// <summary>SND-LINEAR-001: the write key is the read key.</summary>
    internal const string LinearConnSameAsRead = "The write key must differ from the read key. Paste a separate one, made with write permission.";

    /// <summary>SND-LINEAR-014: no keychain.</summary>
    internal const string LinearConnNoKeychain = "No keychain to keep the write key in. Unlock your login keyring, or put the key in 1Password and set writeToken to op://… SND-LINEAR-014";

    /// <summary>SND-LINEAR-014 on Remove key: the keychain entry could not be deleted, so the write key stays.</summary>
    internal const string LinearConnRemoveKeychainFailed = "Couldn't remove the write key from your keychain. Nothing changed: Sindri can still write. Unlock your login keyring and try again.";

    /// <summary>SND-LINEAR-010: Linear did not answer.</summary>
    internal const string LinearConnUnreachable = "Couldn't reach Linear, so nothing was changed. Try again.";

    /// <summary>SND-LINEAR-003: no team picked.</summary>
    internal const string LinearConnNoTeams = "Sindri only writes to teams you picked. Tick at least one.";

    /// <summary>The read of the connection failed before anything was shown.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words, with the code and detail.</returns>
    internal static string LinearConnReadFailed(Error error) => "Couldn't read Linear's connection just now. " + InstanceRefused(error);

    /// <summary>A save or a removal Sindri did not take, other than the codes with their own words.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words, with the code and detail.</returns>
    internal static string LinearConnFailed(Error error) => "Couldn't change Linear's connection. " + InstanceRefused(error);

    /// <summary>The card's summary once a read key is stored.</summary>
    /// <param name="teams">The team keys read.</param>
    /// <param name="writing">True when a write key is stored.</param>
    /// <returns>"Reading ENG · read-only" or "Reading ENG · writing ENG".</returns>
    internal static string LinearConnSummary(IReadOnlyList<string> teams, bool writing)
    {
        var list = string.Join(", ", teams);
        return writing ? $"Reading {list} · writing {list}" : $"Reading {list} · read-only";
    }

    /// <summary>A key line when it works.</summary>
    /// <param name="kind">"Read" or "Write".</param>
    /// <param name="checkedAt">When Sindri last tried the key; null when it never has.</param>
    /// <returns>"Write access: connected · checked 10:05".</returns>
    internal static string LinearConnAccess(string kind, DateTimeOffset? checkedAt) =>
        checkedAt is { } at ? $"{kind} access: connected · checked {at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)}" : $"{kind} access: connected";

    /// <summary>A key line when a pointer is set but no save has checked the key.</summary>
    /// <param name="kind">"Read" or "Write".</param>
    /// <returns>"Read access: Not checked yet. Replace key to check it.".</returns>
    internal static string LinearConnNotChecked(string kind) => $"{kind} access: Not checked yet. Replace key to check it.";

    /// <summary>While a write key is checked.</summary>
    /// <param name="teams">The team keys it has to read.</param>
    /// <returns>"Checking… (can this key read ENG and OPS, and can it write?)".</returns>
    internal static string LinearConnCheckingWrite(IReadOnlyList<string> teams) =>
        teams.Count == 0 ? "Checking… (can this key write?)" : $"Checking… (can this key read {string.Join(" and ", teams)}, and can it write?)";

    /// <summary>A team box's accessible name.</summary>
    /// <param name="name">The team's name.</param>
    /// <param name="key">The team's key.</param>
    /// <returns>"Engineering ENG".</returns>
    internal static string LinearConnTeamName(string name, string key) => $"{name} {key}";
}
