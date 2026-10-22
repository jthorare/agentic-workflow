using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// One row of the Workflows list (screens §4.2): the subject, the inputs, a cell per state, and the served now and chip. It decides
/// nothing: every word is the row's own.
/// </summary>
internal sealed partial class InstanceRowViewModel : ObservableObject
{
    private const string CancelCommand = "ApplyCommand:cancel";

    private readonly InstanceSummary _row;
    private readonly IClipboard _clipboard;
    private readonly TimeProvider _clock;

    internal InstanceRowViewModel(InstanceSummary row, IClipboard clipboard, TimeProvider clock)
    {
        (_row, _clipboard, _clock) = (row, clipboard, clock);
        var blocked = row.Inputs.Any(i => i is { Required: true, Present: false });
        Inputs = [.. row.Inputs.Select(i => new InputCellViewModel(i))];
        var missing = row.Inputs.Count(i => !i.Present);
        MissingInputsText = missing > 0 ? Labels.InputsMissing(missing) : string.Empty;
        Stages = [.. row.States.Select(s =>
        {
            var current = string.Equals(s.State, row.State, StringComparison.Ordinal);
            return new StageCellViewModel(s, current, blocked && current);
        })];
        if (InstanceRules.IsChildPointer(row.Answer))
        {
            ChildAnswer = ChildAnswerViewModel.ForInstance(row.Address, row.Answer.Child);
        }
        else if (InstanceRules.HasProtectedAnswer(row))
        {
            var answer = row.Answer!; // why: HasProtectedAnswer is true only for a row with an answer
            Protected = new ProtectedActionViewModel(new ProtectedAction(Labels.AnswerVerb(answer.Kind), answer.Cli, row.Address), clipboard, clock);
        }

        CanAnswerAtDesk = row.Answer is null && InstanceRules.CompletesAtDesk(row);
        CanCancel = row.Actions.Contains(CancelCommand, StringComparer.Ordinal);
    }

    /// <summary>The instance's address, as served.</summary>
    public string Address => _row.Address;

    /// <summary>The instance's own id, as served.</summary>
    public string InstanceId => _row.Id;

    /// <summary>The state the instance is in, as served.</summary>
    public string StateWord => _row.State;

    /// <summary>The compact line a narrow page shows instead of the columns: the workflow and the stage.</summary>
    public string Compact => $"{DisplayName} · {_row.State}";

    /// <summary>The recipe's id, as served (Details, and what the workflow filter sends).</summary>
    public string Recipe => _row.Recipe.Id;

    /// <summary>The workflow's name as a person reads it: the served display name, else the id made readable.</summary>
    public string DisplayName => WorkflowNames.Of(_row.Recipe.Id, _row.Recipe.DisplayName);

    /// <summary>The stage the run is in, with its round, children and loop partner ("mitigate r1/3 ⇄ verify").</summary>
    public string Stage => StageTag.For(_row);

    /// <summary>Who is on the run, in plain words: you (taken over), paused for a decision, waiting for you, or in progress; nobody once it finished (the chip says how).</summary>
    public string Who => IsFinished ? string.Empty : _row.EngineOwned is { } owned
        ? owned.Kind == EngineOwnedKind.TakenOver ? Labels.WhoYouTookOver : Labels.WhoPaused
        : _row.Answer is not null || InstanceRules.CompletesAtDesk(_row) ? Labels.WhoWaitingForYou : Labels.WhoInProgress;

    /// <summary>"2 inputs missing" while any declared input is missing; empty otherwise.</summary>
    public string MissingInputsText { get; }

    /// <summary>True when any declared input is missing, so the row says so up front.</summary>
    public bool HasMissingInputs => MissingInputsText.Length > 0;

    private bool IsFinished => _row.States.Any(c => c.Terminal && string.Equals(c.State, _row.State, StringComparison.Ordinal));

    /// <summary>The subject: its served label, or its key when it has none.</summary>
    public string Subject => _row.Subject.Label.Length > 0 ? _row.Subject.Label : _row.Subject.Key;

    /// <summary>The served chip.</summary>
    public StateChip Chip => _row.Chip;

    /// <summary>The one status the board, the list and the run drawer show for this run (F3 workflows spec §4).</summary>
    public RunStatus Status => RunStatus.From(_row.Chip);

    /// <summary>How long the run has been in its stage ("in stage 1h 30m"), or how long ago it ended ("ended 5h 20m ago"); the time is the served since.</summary>
    public string TimeText => IsFinished ? Labels.WorkflowsEndedAgo(Age) : Labels.WorkflowsInStage(Age);

    private TimeSpan Age => _clock.GetUtcNow() - _row.Since;

    /// <summary>The served line stage: the state id of the root line instance the run belongs to; null when its root is not a line run.</summary>
    public string? LineStage => _row.LineStage;

    /// <summary>The served now sentence, unchanged.</summary>
    public string Now => _row.Now;

    /// <summary>A cell per declared input.</summary>
    public IReadOnlyList<InputCellViewModel> Inputs { get; }

    /// <summary>A cell per state, in the served order.</summary>
    public IReadOnlyList<StageCellViewModel> Stages { get; }

    /// <summary>True when the row declares inputs; with none the inputs column has nothing to name and stays out of the control view.</summary>
    public bool HasInputs => Inputs.Count > 0;

    /// <summary>What a screen reader hears for the inputs column: the cells' names in one.</summary>
    public string InputsName => string.Join(", ", Inputs.Select(i => i.Name));

    /// <summary>What a screen reader hears for the stages column: the cells' names in one.</summary>
    public string StagesName => string.Join(", ", Stages.Select(c => c.Name));

    /// <summary>True while the row's Details show (the inputs, every stage, the workflow type's id and the run's address); closed until opened.</summary>
    public bool IsDetailsOpen { get; set => Set(ref field, value); }

    /// <summary>The Details disclosure's header.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string DetailsText => Labels.RowDetails;

    /// <summary>The stages line's heading in Details.</summary>
    public string StagesHeading => Labels.StagesHeading;

    /// <summary>The inputs line's heading in Details.</summary>
    public string InputsHeading => Labels.InputsDetailsHeading;

    /// <summary>The workflow type id line's heading in Details.</summary>
    public string RecipeHeading => Labels.WorkflowTypeIdHeading;

    /// <summary>The run address line's heading in Details.</summary>
    public string AddressHeading => Labels.RunAddressHeading;

    /// <summary>The Copy button's words.</summary>
    public string CopyText => Labels.Copy;

    /// <summary>The Copy button's accessible name: the visible word, then what it copies.</summary>
    public string CopyName => $"{Labels.Copy} {Labels.RunAddressHeading.ToLowerInvariant()}";
#pragma warning restore CA1822, MA0041

    /// <summary>Opens the Details, or closes them.</summary>
    internal void ToggleDetails() => IsDetailsOpen = !IsDetailsOpen;

    /// <summary>Puts the run's address on the clipboard.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the clipboard was asked.</returns>
    internal Task CopyAddress(CancellationToken cancellationToken) => _clipboard.SetText(_row.Address, cancellationToken);

    /// <summary>The title button's accessible name: the visible title first, then the workflow.</summary>
    public string OpenName => Labels.OpenRunName(Subject, DisplayName);

    /// <summary>The name screen readers hear: the item (the visible title), the workflow, the served chip word, the stage and who is on it. The address is in Details.</summary>
    public string AccessibleName => string.Join(", ", new[] { Subject, DisplayName, _row.Chip.Word, _row.State, Who, MissingInputsText }.Where(p => p.Length > 0));

    /// <summary>Where Enter and a click go: the instance in the Workflows frame.</summary>
    public DeepLink Link => new(DeepLinkKind.Workflow, _row.Address);

    /// <summary>The lock button for a protected answer; null for none.</summary>
    public ProtectedActionViewModel? Protected { get; }

    /// <summary>The "Answer in the child" control of a row whose answer a child decides; null for any other row.</summary>
    public ChildAnswerViewModel? ChildAnswer { get; }

    /// <summary>True when A answers at the desk: the row's current state is a desk state the engine lets the human complete.</summary>
    public bool CanAnswerAtDesk { get; }

    /// <summary>True when the engine lists Cancel for the row (X asks).</summary>
    public bool CanCancel { get; }

    /// <summary>True for the row the keys act on.</summary>
    public bool IsSelected { get; internal set => Set(ref field, value); }

    /// <summary>True while the views are stalled or rebuilding: the row stays, marked stale.</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>Everything the row draws: two rows with the same signature look the same.</summary>
    internal string Signature => string.Join('|', Address, StageName, _row.Since.UtcTicks, DisplayName, Who, MissingInputsText, Subject, Chip, Now, string.Join(',', Inputs.Select(i => i.Name)), string.Join(',', Stages.Select(s => s.Name)), StateWord, Chip.Word, Protected?.Cli, ChildAnswer?.Target, ChildAnswer?.Intent, CanAnswerAtDesk, CanCancel);

    /// <summary>Reads the time in stage anew.</summary>
    internal void Tick() => OnChanged(nameof(TimeText));

    /// <summary>Sets what the list knows of the row.</summary>
    /// <param name="stale">The views are stalled or rebuilding.</param>
    internal void SetStale(bool stale) => IsStale = stale;
}
