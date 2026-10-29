using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sandbox;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>
/// The item sheet's Blocked list: what the sandbox denied the item's launches, counted per target and repo over every launch, with a fix per row
/// (states.json 'blockedList'). A failed read keeps what was shown; a re-read keeps the row objects so the focused Allow button survives.
/// </summary>
internal sealed class BlockedListViewModel(ISindriApi api) : ObservableObject
{
    private const int Top = 5;

    private readonly Dictionary<string, BlockedRowViewModel> _known = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _approvedWhenAsked = new(StringComparer.Ordinal);
    private List<BlockedTarget> _targets = [];
    private bool _showAll;
    private bool _read;
    private string _item = string.Empty;
    private Uri? _url;

    /// <summary>The list's heading.</summary>
    public string Heading { get; } = Labels.BlockedHeading;

    /// <summary>"Nothing was blocked" or "Blocked: top 5 of 12"; empty for a short list.</summary>
    public string Summary { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The rows shown: every target of a short list, the top five of a long one.</summary>
    public IReadOnlyList<BlockedRowViewModel> Rows { get; private set => Set(ref field, value); } = [];

    /// <summary>True when a long list shows only its top five.</summary>
    public bool HasSeeAll { get; private set => Set(ref field, value); }

    /// <summary>True once the item's denials were read: the section shows, with its nothing-blocked sentence when there is none.</summary>
    public bool IsShown { get; private set => Set(ref field, value); }

    /// <summary>"See all blocked".</summary>
    public string SeeAllText { get; } = Labels.SeeAllBlocked;

    /// <summary>The one polite live region of the list and its Allow buttons.</summary>
    public LiveAnnouncer Voice { get; } = new(Labels.AllowResting);

    /// <summary>Raised when an Allow wants the owner to open the profile approval.</summary>
    internal event EventHandler<ApprovalRequestedEventArgs>? ApprovalRequested;

    /// <summary>Shows every target.</summary>
    internal void ShowAll()
    {
        _showAll = true;
        Draw();
    }

    /// <summary>Forgets the item (the sheet moved to another).</summary>
    internal void Reset()
    {
        (_item, _showAll, _read) = (string.Empty, false, false);
        _targets = [];
        _known.Clear();
        _approvedWhenAsked.Clear();
        Draw();
    }

    /// <summary>Reads the item's denials; first checks whether an open approval was answered.</summary>
    /// <param name="url">The API.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that ends when the list was drawn.</returns>
    internal async Task Read(Uri url, string itemId, CancellationToken cancellationToken)
    {
        if (!string.Equals(_item, itemId, StringComparison.Ordinal))
        {
            Reset();
            _item = itemId;
        }

        _url = url;
        await SettleApproved(url, cancellationToken).ConfigureAwait(true);
        var answer = await api.SandboxDenials(url, itemId, cancellationToken).ConfigureAwait(true);
        if (answer.IsError || !string.Equals(_item, itemId, StringComparison.Ordinal))
        {
            return;
        }

        (_targets, _read) = (Targets(answer.Value), true);
        Draw();
    }

    // why: an Allow is a draft profile edit until the owner approves the profile. A row settles only when nothing is left to approve AND the
    // approved hash moved past the one first seen while the row waited: a discarded or reverted draft also leaves nothing to approve, but
    // keeps the old approved hash, so the host stays "needs approval" instead of reading "Allowed" while it is still blocked.
    private async Task SettleApproved(Uri url, CancellationToken cancellationToken)
    {
        var waiting = _known.Values.Where(r => r.IsWaitingForApproval).ToList();
        if (waiting.Count == 0)
        {
            return;
        }

        var profile = await api.ProfileSummary(url, cancellationToken).ConfigureAwait(true);
        if (profile.IsError)
        {
            return;
        }

        foreach (var row in waiting)
        {
            if (!_approvedWhenAsked.TryGetValue(row.Key, out var asked))
            {
                _approvedWhenAsked[row.Key] = profile.Value.Approved;
            }
            else if (profile.Value.Summary is null && !string.Equals(asked, profile.Value.Approved, StringComparison.Ordinal))
            {
                row.Settle();
                _ = _approvedWhenAsked.Remove(row.Key);
            }
        }
    }

    private static List<BlockedTarget> Targets(DenialsView view)
    {
        var byKey = new Dictionary<string, BlockedTarget>(StringComparer.Ordinal);
        var best = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var launch in view.Launches)
        {
            foreach (var entry in launch.Entries)
            {
                var target = new BlockedTarget(entry.Kind, entry.Target, entry.Count, FixOf(entry), entry.Reason ?? string.Empty, launch.LaunchId, launch.Repo);
                if (!byKey.TryGetValue(target.Key, out var seen))
                {
                    (byKey[target.Key], best[target.Key]) = (target, entry.Count);
                    continue;
                }

                var strongest = entry.Count > best[target.Key];
                best[target.Key] = Math.Max(best[target.Key], entry.Count);
                byKey[target.Key] = seen with { Count = seen.Count + entry.Count, Launch = strongest ? launch.LaunchId : seen.Launch };
            }
        }

        return [.. byKey.Values];
    }

    // A server before the field: a host can be allowed, a tool is the owner's, a path is not offered.
    private static string FixOf(DenialEntryView entry) => entry.Fix ?? entry.Kind switch
    {
        "network" => "allow",
        "policy" => "own-terminal",
        _ => "never",
    };

    private void Draw()
    {
        var shown = _targets.Count > Top && !_showAll
            ? [.. _targets.Select((t, i) => (t, i)).OrderByDescending(p => p.t.Count).ThenBy(p => p.i).Take(Top).OrderBy(p => p.i).Select(p => p.t)]
            : _targets;
        var rows = shown.Select(Row).ToList();
        if (!rows.SequenceEqual(Rows))
        {
            Rows = rows;
        }

        HasSeeAll = _targets.Count > Top && !_showAll;
        IsShown = _item.Length > 0 && _read;
        Summary = _item.Length == 0 ? string.Empty
            : _targets.Count == 0 ? Labels.BlockedNothing
            : HasSeeAll ? Labels.BlockedMany(_targets.Count) : string.Empty;
    }

    private BlockedRowViewModel Row(BlockedTarget target)
    {
        if (_known.TryGetValue(target.Key, out var row))
        {
            row.Update(target);
            return row;
        }

        return _known[target.Key] = new BlockedRowViewModel(target, (request, token) => api.SandboxAllow(_url ?? throw new InvalidOperationException("a row exists only after a read"), request, token), Voice, args => ApprovalRequested?.Invoke(this, args));
    }
}
