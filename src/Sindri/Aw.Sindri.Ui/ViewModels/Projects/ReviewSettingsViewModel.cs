using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// The project sheet's Review settings (review page spec §7 and Ph9, D5): the settings entry the project's PRs get, each key with where it comes from, the editors for panels, groups,
/// collapse, risky paths and checks, the project's repos, and Save and approve. A key left at its inherited value is not written into the entry. Read on the sheet's open and its events,
/// but never over edits in progress. The wire records are the <c language="text">ReviewSettings*</c> files under Api/Reviews.
/// </summary>
/// <param name="api">The API.</param>
internal sealed partial class ReviewSettingsViewModel(ISindriApi api) : ObservableObject
{
    internal const string PanelsKeyId = "panels";
    internal const string GroupsKeyId = "groups";
    internal const string CollapseKeyId = "collapse";
    internal const string RiskyKeyId = "risky";
    internal const string ChecksKeyId = "checks";

    private ReviewSettingsRead? _read;
    private Uri? _url;
    private string? _snapshot;
    private int _generation;

    /// <summary>The labels the view binds.</summary>
    public ReviewSettingsWords Words { get; } = new();

    /// <summary>True once the settings are read (the section is hidden before that and when they can't be read).</summary>
    public bool IsShown { get; private set => Set(ref field, value); }

    /// <summary>"Settings entry: sindri".</summary>
    public string EntryLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Also used by …", or empty.</summary>
    public string SharedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The settings entry's name being edited.</summary>
    public string Name { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The entry this one builds on being edited; empty for none.</summary>
    public string Extends { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The project's repos being edited, one per line.</summary>
    public string ReposText { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The server's detail under the name field.</summary>
    public string NameError { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The server's detail under the extends field.</summary>
    public string ExtendsError { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The server's detail under the repos field.</summary>
    public string ReposError { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The panels key.</summary>
    public ReviewSettingsKey PanelsKey { get; } = new(PanelsKeyId, Labels.ReviewSettingsPanels);

    /// <summary>The groups key.</summary>
    public ReviewSettingsKey GroupsKey { get; } = new(GroupsKeyId, Labels.ReviewSettingsGroups);

    /// <summary>The collapse key.</summary>
    public ReviewSettingsKey CollapseKey { get; } = new(CollapseKeyId, Labels.ReviewSettingsCollapse);

    /// <summary>The risky key.</summary>
    public ReviewSettingsKey RiskyKey { get; } = new(RiskyKeyId, Labels.ReviewSettingsRisky);

    /// <summary>The checks key.</summary>
    public ReviewSettingsKey ChecksKey { get; } = new(ChecksKeyId, Labels.ReviewSettingsChecks);

    /// <summary>Every panel in the order the editor shows: the shown ones first.</summary>
    public IReadOnlyList<SettingsPanelRow> PanelRows { get; private set => Set(ref field, value); } = [];

    /// <summary>The diff groups.</summary>
    public IReadOnlyList<SettingsGroupRow> GroupRows
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(ShowGroupsByFolder));
        }
    } = [];

    /// <summary>True while no group is set, so the default's by-folder grouping applies.</summary>
    public bool ShowGroupsByFolder => GroupRows.Count == 0;

    /// <summary>The folded file globs, one per line.</summary>
    public string CollapseText { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The risky paths.</summary>
    public IReadOnlyList<SettingsRiskRow> RiskRows { get; private set => Set(ref field, value); } = [];

    /// <summary>The required checks.</summary>
    public IReadOnlyList<SettingsCheckRow> CheckRows { get; private set => Set(ref field, value); } = [];

    /// <summary>True while the settings take edits; false after a 403.</summary>
    public bool CanChange
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(IsReadOnly));
            OnChanged(nameof(CanEdit));
            OnChanged(nameof(CanSave));
        }
    } = true;

    /// <summary>True while the fields, rows and buttons take input: the settings are the owner's to change and no save is in flight (a save's re-read would wipe an edit made meanwhile).</summary>
    public bool CanEdit => CanChange && !IsSaving;

    /// <summary>True after the server refused the owner's write: the settings show but don't edit.</summary>
    public bool IsReadOnly => !CanChange;

    /// <summary>True while a save is in flight.</summary>
    public bool IsSaving
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(SaveText));
            OnChanged(nameof(CanEdit));
        }
    }

    /// <summary>"Save and approve", or "Saving…" while the write is in flight.</summary>
    public string SaveText => IsSaving ? Labels.ProjectSaving : Labels.ReviewSettingsSaveApprove;

    /// <summary>True when the settings on screen can't be trusted until Reload: the profile changed since they were read, or a save went through but the read-back failed. Save is off meanwhile.</summary>
    public bool IsStale
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CanSave));
        }
    }

    /// <summary>The line above Reload: why the settings need reading again.</summary>
    public string StaleText { get; private set => Set(ref field, value); } = Labels.ReviewSettingsStale;

    /// <summary>True while Save can be pressed: the settings are the owner's to change and none is stale.</summary>
    public bool CanSave => CanChange && !IsStale;

    /// <summary>The line after a save that waits for a profile approval; empty otherwise.</summary>
    public string Note { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Under the buttons: why the save failed, when no field is named; empty otherwise.</summary>
    public string SaveError { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The last finished save in words; the shell says it.</summary>
    public string Announcement { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when anything differs from what was read.</summary>
    public bool HasChanges => _snapshot is not null && !string.Equals(_snapshot, Snapshot(), StringComparison.Ordinal);

    /// <summary>Reads the project's settings and shows them; an edit or a save in progress keeps what it has, and a failed read leaves the section hidden.</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="project">The project's id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal async Task Open(Uri url, string project, CancellationToken cancellationToken)
    {
        _url = url;
        if (HasChanges || IsSaving)
        {
            return;
        }

        _ = await Read(project, discard: false, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Drops every edit and reads the settings again.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Discard(CancellationToken cancellationToken) =>
        _read is { } read && !IsSaving ? Read(read.Project, discard: true, cancellationToken) : Task.CompletedTask;

    /// <summary>Forgets the project: another sheet is opening.</summary>
    internal void Clear()
    {
        _generation++;
        (_read, _snapshot) = (null, null);
        (IsShown, CanChange, IsSaving) = (false, true, false);
        (Note, SaveError, Announcement) = (string.Empty, string.Empty, string.Empty);
    }

    // Reads and draws; false when the settings can't be read. Unless the owner asked to discard, a read never draws over text typed while it was out.
    private async Task<bool> Read(string project, bool discard, CancellationToken cancellationToken)
    {
        if (_url is not { } url)
        {
            return false;
        }

        var generation = ++_generation;
        var before = _snapshot is null ? null : Snapshot();
        var answer = await api.GetReviewSettings(url, project, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return true;   // another project took over
        }

        if (answer.IsError)
        {
            return false;   // the section stays as it was
        }

        if (discard || before is null || string.Equals(before, Snapshot(), StringComparison.Ordinal))
        {
            Draw(answer.Value);
        }

        return true;
    }

    private void Draw(ReviewSettingsRead read)
    {
        _read = read;
        var resolved = read.Resolved;
        Name = read.Settings;
        EntryLine = Labels.ReviewSettingsEntryLine(read.Settings);
        SharedText = read.SharedWith.Count > 0 ? Labels.ReviewSettingsSharedWith(string.Join(", ", read.SharedWith)) : string.Empty;
        Extends = read.Entry.Extends ?? string.Empty;
        ReposText = Join(read.Repos);
        PanelRows = [.. resolved.Panels.Concat(read.AllPanels.Except(resolved.Panels, StringComparer.Ordinal)).Select(id => new SettingsPanelRow(id, resolved.Panels.Contains(id, StringComparer.Ordinal)))];
        GroupRows = [.. resolved.Groups.Select((g, i) => new SettingsGroupRow(i + 1, g.Name, Join(g.Globs)))];
        CollapseText = Join(resolved.Collapse);
        RiskRows = [.. resolved.Risky.Select((r, i) => new SettingsRiskRow(i + 1, Join(r.Globs), r.Why))];
        CheckRows = [.. resolved.Checks.Select((c, i) => new SettingsCheckRow(i + 1, c.Id, c.Title, Join(c.When ?? []), ReviewSettingsNeed.Text(c.Need), c.Need))];
        var entry = read.Entry;
        Source(PanelsKey, entry.Panels is not null);
        Source(GroupsKey, entry.Groups is not null);
        Source(CollapseKey, entry.Collapse is not null);
        Source(RiskyKey, entry.Risky is not null);
        Source(ChecksKey, entry.Checks is not null);
        ClearErrors();
        IsShown = true;
        _snapshot = Snapshot();
    }

    // Where a key's value comes from: this entry when it sets the key, else the entry the API names.
    private void Source(ReviewSettingsKey key, bool own)
    {
        key.WasOwn = own;
        key.IsReset = false;
        var from = _read!.Resolved.Sources.TryGetValue(key.Id, out var name) ? name : "default";   // why: Draw sets _read first
        key.FromText = string.Equals(from, "default", StringComparison.Ordinal) ? Labels.ReviewSettingsFromDefault
            : string.Equals(from, _read.Settings, StringComparison.Ordinal) ? Labels.ReviewSettingsFromEntry : Labels.ReviewSettingsFromOther(from);
        key.Baseline = Signature(key.Id);
    }

    private void ClearErrors()
    {
        (NameError, ExtendsError, ReposError, SaveError) = (string.Empty, string.Empty, string.Empty, string.Empty);
        IsStale = false;
        foreach (var key in Keys)
        {
            key.Error = string.Empty;
        }
    }

    private ReviewSettingsKey[] Keys => [PanelsKey, GroupsKey, CollapseKey, RiskyKey, ChecksKey];

    internal static string Join(IEnumerable<string> lines) => string.Join('\n', lines);

    internal static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // A key's value as one string, to tell whether the owner changed it.
    private string Signature(string id) => id switch
    {
        PanelsKeyId => string.Join('|', PanelRows.Where(p => p.Visible).Select(p => p.Id)),
        GroupsKeyId => string.Join('|', GroupRows.Where(Filled).Select(g => $"{g.Name}\u001f{g.GlobsText}")),
        CollapseKeyId => CollapseText,
        RiskyKeyId => string.Join('|', RiskRows.Where(Filled).Select(r => $"{r.GlobsText}\u001f{r.Why}")),
        _ => string.Join('|', CheckRows.Where(Filled).Select(c => $"{c.Id}\u001f{c.Title}\u001f{c.WhenText}\u001f{c.NeedText}")),
    };

    // A row the owner added and left empty is dropped when sending, so it is not a change either.
    internal static bool Filled(SettingsGroupRow row) => row.Name.Trim().Length > 0 || row.GlobsText.Trim().Length > 0;

    internal static bool Filled(SettingsRiskRow row) => row.GlobsText.Trim().Length > 0 || row.Why.Trim().Length > 0;

    internal static bool Filled(SettingsCheckRow row) => row.Id.Trim().Length > 0 || row.Title.Trim().Length > 0 || row.NeedText.Trim().Length > 0;

    private string Snapshot() =>
        string.Join('\u001e', [Name, Extends, ReposText, .. Keys.Select(k => k.IsReset + Signature(k.Id))]);
}
