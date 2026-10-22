using System.Globalization;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Facts;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// The instance frame (screens §4.2): one instance, read by its opaque id. A frame opened from a row already has the id;
/// one opened by a deep link has only the address, which the list resolves first (an address is never a path segment).
/// The header is the status header's served words, the subject line, the states band and the expanded state are the
/// instance's served fields; a not-found or unreadable answer shows its sentence with Back. A re-read keeps what is
/// shown when the answer fails or the instance has ended, and the newest read wins.
/// </summary>
internal sealed partial class InstanceFrameViewModel(ISindriApi api, IClipboard clipboard, TimeProvider clock, FactViewerRegistry? viewers = null, CatalogViewModel? catalog = null, ILauncher? launcher = null) : ObservableObject
{
    private readonly ILauncher _launcher = launcher ?? NullLauncher.Instance;
    private Uri? _url;
    private NeedsYou.InstanceCommander? _commander;
    private string? _id;
    private int _generation;
    private (string Address, FrameIntent Intent, string? Key)? _intent;

    /// <summary>Raised when the frame asks to open another instance.</summary>
    internal event EventHandler<FrameOpenEventArgs>? OpenRequested;

    /// <summary>Where the read stands.</summary>
    public FramePhase Phase
    {
        get;
        private set
        {
            Set(ref field, value);
            foreach (var name in (string[])[nameof(IsLoading), nameof(IsShown), nameof(IsProblem), nameof(InitialFocus)])
            {
                OnChanged(name);
            }
        }
    }

    /// <summary>True before the first answer.</summary>
    public bool IsLoading => Phase == FramePhase.Loading;

    /// <summary>True while an instance is shown.</summary>
    public bool IsShown => Phase == FramePhase.Shown;

    /// <summary>True while a refusal or an unreadable answer is shown.</summary>
    public bool IsProblem => Phase is FramePhase.NotFound or FramePhase.Unreadable or FramePhase.Refused;

    /// <summary>The instance's address.</summary>
    public string Address { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The title as served.</summary>
    public string Title { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The recipe's id.</summary>
    public string Recipe { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The first twelve characters of the recipe's hash, as served.</summary>
    public string Hash12 { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"v12".</summary>
    public string Version { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The state word the status header serves.</summary>
    public string StateText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The header's detail, or empty.</summary>
    public string Detail { get; private set { Set(ref field, value); OnChanged(nameof(HasDetail)); } } = string.Empty;

    /// <summary>True when the header serves a detail.</summary>
    public bool HasDetail => Detail.Length > 0;

    /// <summary>"since 09:14 (3h 12m)".</summary>
    public string SinceText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The header's line under the title: the workflow's name, the stage (with its detail) and since.</summary>
    public string Identity { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The subject line; null before a read.</summary>
    public SubjectLineViewModel? Subject { get; private set { Set(ref field, value); OnChanged(nameof(InitialFocus)); } }

    /// <summary>The states band; null before a read.</summary>
    public StatesBandViewModel? Band { get; private set => Set(ref field, value); }

    /// <summary>The expanded state; null before a read.</summary>
    public ExpandedStateViewModel? Expanded { get; private set => Set(ref field, value); }

    /// <summary>The Step runs; null before a read.</summary>
    public StepRunsViewModel? StepRuns { get; private set => Set(ref field, value); }

    /// <summary>The sentence a refusal or an unreadable answer shows.</summary>
    public string ProblemText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The note under the header (done, or no longer served); empty when none.</summary>
    public string Note { get; private set { Set(ref field, value); OnChanged(nameof(HasNote)); } } = string.Empty;

    /// <summary>True when there is a note.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>Back's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string BackText => Labels.Back;

    /// <summary>The subject line's lead word.</summary>
    public string SubjectText => Labels.SubjectLead;

    /// <summary>The heading of the loading skeleton.</summary>
    public string LoadingText => Labels.LoadingInstance;
#pragma warning restore CA1822, MA0041

    /// <summary>Where focus lands: always a read action (the subject's Open in, else Copy status), Back when there is nothing else (§11.2).</summary>
    public FrameFocus InitialFocus => Phase == FramePhase.Shown ? (Subject is { HasHref: true } ? FrameFocus.OpenIn : FrameFocus.CopyStatus) : FrameFocus.Back;

    /// <summary>Names the instance now, before any read (a deep link, even before the app is connected).</summary>
    /// <param name="address">The address.</param>
    /// <param name="id">The id when known.</param>
    internal void Show(string address, string? id)
    {
        if (!string.Equals(address, Address, StringComparison.Ordinal))
        {
            Clear();
            Address = address;
            _id = id;
            Phase = FramePhase.Loading;
        }
        else if (id is not null)
        {
            _id = id;
        }
    }

    /// <summary>Opens an instance: resolves the address when no id is known, then reads by id.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="address">The address.</param>
    /// <param name="id">The id when known.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the frame shows the answer.</returns>
    internal Task Open(Uri url, string address, string? id, CancellationToken cancellationToken)
    {
        Show(address, id);
        _url = url;
        return Read(cancellationToken);
    }

    /// <summary>Re-reads the instance shown (on every event).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the frame shows the answer.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return Address.Length == 0 ? Task.CompletedTask : Read(cancellationToken);
    }

    private async Task Read(CancellationToken cancellationToken)
    {
        if (_url is not { } url)
        {
            return;
        }

        var (address, generation) = (Address, ++_generation);
        if (_id is null)
        {
            if (await Resolve(url, address, generation, cancellationToken).ConfigureAwait(true) is not { } resolved)
            {
                return;   // not found, failed, or another instance took over: whatever it was, it must not clear the id of the newer one
            }

            _id = resolved;
        }

        var answer = await api.Instance(url, _id, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;   // a newer read, or another instance, took over
        }

        if (answer.IsSuccess)
        {
            Show(answer.Value);
            await ReadRun(answer.Value, generation, cancellationToken).ConfigureAwait(true);
        }
        else
        {
            Failed(answer.Error);
        }
    }

    // An address from a deep link names a row of the list: the one whose address it is, else the one it matches (an item id, an instance id).
    private async Task<string?> Resolve(Uri url, string address, int generation, CancellationToken cancellationToken)
    {
        var found = await api.Instances(url, new InstanceQuery(Address: address), cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return null;
        }

        if (found.IsError)
        {
            Failed(found.Error);
            return null;
        }

        var rows = found.Value.Instances;
        // The row whose address it is; else the one row the link matches (an item id, an instance id); a link that matches several opens none of them.
        if ((rows.FirstOrDefault(r => string.Equals(r.Address, address, StringComparison.Ordinal)) ?? (rows.Count == 1 ? rows[0] : null)) is not { } row)
        {
            Problem(FramePhase.NotFound, Labels.InstanceNotFound(address));
            return null;
        }

        return row.Id;
    }

    private void Show(InstanceDetail detail)
    {
        var header = detail.Current.Status.Header;
        var local = TimeZoneInfo.ConvertTime(header.Since, clock.LocalTimeZone);
        Address = detail.Address;
        _state = detail.State;
        (Title, Recipe, Hash12, Version) = (detail.Title, detail.Recipe.Id, header.Hash12, string.Create(CultureInfo.InvariantCulture, $"v{header.Version}"));
        (StateText, Detail) = (header.State, header.Detail ?? string.Empty);
        OnChanged(nameof(RecipeSubject));
        OnChanged(nameof(HasRecipeLink));
        OnChanged(nameof(RecipeLinkName));
        SinceText = $"since {local.ToString("HH:mm", CultureInfo.InvariantCulture)} ({Labels.Duration(header.AgeSeconds)})";
        WorkflowName = WorkflowNameOf(detail);
        DetailsText = Labels.DetailsBlock(detail.Address, Recipe, Hash12, Version);
        AddressCopy = new CopyAction(clipboard, clock, Labels.CopyAddressName(detail.Address));
        Identity = $"{WorkflowName} · {StateText}{(HasDetail ? ": " + Detail : string.Empty)} · {SinceText}";
        var terminal = detail.States.FirstOrDefault(s => s.Terminal && string.Equals(s.State, detail.State, StringComparison.Ordinal));
        Note = terminal is null ? string.Empty : Labels.InstanceFinished(terminal.Outcome);
        ProblemText = string.Empty;
        ShowParts(detail);
        Phase = FramePhase.Shown;
        ApplyIntent();
    }

    // A re-read serves the same parts again: they stay (and the control with focus does) unless something they draw changed.
    private void ShowParts(InstanceDetail detail)
    {
        var subject = new SubjectLineViewModel(detail.Subject, detail.Address, _launcher);
        if (Subject is null || !string.Equals(Subject.Signature, subject.Signature, StringComparison.Ordinal))
        {
            Subject = subject;
        }

        var band = new StatesBandViewModel(detail.States, detail.OrderedBy, detail.State);
        if (Band is null || !string.Equals(Band.Signature, band.Signature, StringComparison.Ordinal))
        {
            Band = band;
        }

        ShowPanels(detail);
        ShowStepRuns(detail);
        var expanded = new ExpandedStateViewModel(detail.Current, detail.Address, clipboard, clock) { Commander = _commander ??= new NeedsYou.InstanceCommander(api, () => _url), InstanceId = detail.Id };
        if (Expanded is null || !string.Equals(Expanded.Signature, expanded.Signature, StringComparison.Ordinal))
        {
            expanded.ChildRequested += (_, e) => OpenRequested?.Invoke(this, e);
            expanded.SessionRequested += (_, e) => SessionRequested?.Invoke(this, e);
            expanded.ChildrenRequested += (_, _) => Focus(ChildrenFocus, key: null);
            Expanded = expanded;
        }
        else
        {
            Expanded.Update(detail.Current);
        }
    }

    private void Clear()
    {
        _generation++;
        (Title, Recipe, Hash12, Version, StateText, Detail, SinceText, Identity, ProblemText, Note) = (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        (WorkflowName, DetailsText, AddressCopy) = (string.Empty, string.Empty, null);
        (Subject, Band, Expanded, StepRuns) = (null, null, null, null);
        ClearPanels();
        (_eventsFor, _eventRows) = (null, []);
        WantedFocus = null;
        Phase = FramePhase.Empty;
    }
}
