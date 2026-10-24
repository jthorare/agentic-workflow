using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>
/// The command catalog as controls (UI spec §9.1: the only list of actions the app knows), read on connect and after a
/// reconnect. Each row becomes a <see cref="ControlViewModel"/>; the actions the app has a sender for run, and the rest
/// show their command to copy. Health lists the copy-only rows (§4.8) and the palette lists every row (§8).
/// </summary>
/// <param name="clipboard">Where Copy puts a CLI.</param>
/// <param name="clock">The clock for the controls' timings.</param>
internal sealed class CatalogViewModel(IClipboard clipboard, TimeProvider clock) : ObservableObject
{
    /// <summary>The catalog as last read; null before the first. Rows that carry the readiness of an action the API names.</summary>
    internal CommandList? Listed { get; private set; }

    /// <summary>Where Copy puts a CLI (a decision's line copies through it too).</summary>
    internal IClipboard Clipboard { get; } = clipboard;

    private Dictionary<string, CommandRowView> _rows = new(StringComparer.Ordinal);

    /// <summary>Every catalog row, in the API's order.</summary>
    public IReadOnlyList<ControlViewModel> Controls { get; private set => Set(ref field, value); } = [];

    /// <summary>The rows the app has no form for (copy blocks).</summary>
    public IReadOnlyList<ControlViewModel> CopyOnly { get; private set => Set(ref field, value); } = [];

    /// <summary>The Rebuild views control, or null before the catalog is read.</summary>
    public ControlViewModel? Rebuild { get; private set => Set(ref field, value); }

    /// <summary>The Stop Sindri control (destructive: it asks first), or null before the catalog is read.</summary>
    public ControlViewModel? Stop { get; private set => Set(ref field, value); }

    /// <summary>"49 in this app, 14 to copy", or empty before the catalog is read.</summary>
    public string Counts { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The control for an action id, or null when the catalog has no such row.</summary>
    /// <param name="action">The action id.</param>
    /// <returns>The control.</returns>
    internal ControlViewModel? For(string action) => Controls.FirstOrDefault(c => string.Equals(c.Action, action, StringComparison.Ordinal));

    /// <summary>The catalog row for an action id, or null when the catalog is not read or has no such row.</summary>
    /// <param name="action">The action id.</param>
    /// <returns>The row.</returns>
    internal CommandRowView? Row(string action) => _rows.GetValueOrDefault(action);

    /// <summary>A control for one catalog row acting on one subject (an item's actions, a row's primary action), or null when the catalog has no such row.</summary>
    /// <param name="action">The action id.</param>
    /// <param name="subject">The subject's id, named in the control's accessible name.</param>
    /// <param name="send">Sends the command once; null when nothing is wired to it.</param>
    /// <param name="confirm">True when the app asks first even though the catalog does not call the command destructive (a spec'd Confirm and Keep, as on Cancel job).</param>
    /// <returns>The control, or null.</returns>
    internal ControlViewModel? On(string action, string subject, Func<CancellationToken, Task<Result<CommandAcceptedView>>>? send, bool confirm = false) =>
        _rows.TryGetValue(action, out var row) ? new ControlViewModel(confirm ? row with { Destructive = true } : row, send, Clipboard, clock, subject) : null;

    /// <summary>Shows a catalog read from the API at <paramref name="api"/>.</summary>
    /// <param name="api">Where the API answers (the senders call it).</param>
    /// <param name="client">The API.</param>
    /// <param name="catalog">The catalog.</param>
    internal void Show(Uri api, ISindriApi client, CommandList catalog)
    {
        Listed = catalog;
        _rows = catalog.Rows.ToDictionary(r => r.Action, StringComparer.Ordinal);
        Controls = [.. catalog.Rows.Select(row => new ControlViewModel(row, Sender(api, client, row.Action), Clipboard, clock))];
        CopyOnly = [.. Controls.Where(c => c.IsCopyOnly)];
        (Rebuild, Stop) = (For("RebuildViews"), For("StopProcess"));
        Counts = Labels.ActionCounts(Controls.Count - CopyOnly.Count, CopyOnly.Count);
    }

    // The actions the catalog's own list can send. A service's actions are per row (On); every other row shows its
    // command to copy until it has a form. StopProcess is destructive: its control asks first.
    private static Func<CancellationToken, Task<Result<CommandAcceptedView>>>? Sender(Uri api, ISindriApi client, string action) => action switch
    {
        "RebuildViews" => ct => client.RebuildViews(api, ct),
        "StopProcess" => async ct => (await client.StopSindri(api, ct).ConfigureAwait(true)).Map(stopped => new CommandAcceptedView("stop", stopped.Sequence)),
        _ => null,
    };
}
