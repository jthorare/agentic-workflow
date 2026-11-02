using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// One line of the Home Workflows strip (screens §4.1): the served chip, subject, recipe, stage tag, "now" sentence and age,
/// and at most one control: the desk answer, or for a protected answer the lock button with its popover. It decides nothing:
/// every word is the row's own, and the control exists only when the row's served actions and answer allow it.
/// </summary>
internal sealed class StripRowViewModel : ObservableObject
{
    private readonly InstanceSummary _row;
    private readonly TimeProvider _clock;

    internal StripRowViewModel(InstanceSummary row, IClipboard clipboard, TimeProvider clock)
    {
        (_row, _clock) = (row, clock);
        var answer = row.Answer;
        if (InstanceRules.IsChildPointer(answer))
        {
            ChildAnswer = ChildAnswerViewModel.ForInstance(row.Address, answer.Child);
        }
        else if (answer is not null && InstanceRules.HasProtectedAnswer(row))
        {
            Protected = new ProtectedActionViewModel(new ProtectedAction(Labels.AnswerVerb(answer.Kind), answer.Cli, row.Address), clipboard, clock);
        }

        AtDesk = answer is null && InstanceRules.CompletesAtDesk(row);
        CanAnswer = Protected is null && ChildAnswer is null && (answer is not null || AtDesk);
    }

    /// <summary>The served chip (the word and tone the chip control draws).</summary>
    public StateChip Chip => _row.Chip;

    /// <summary>The one status the row shows, mapped from the served chip (the shared badge: a glyph and a sentence-case word).</summary>
    public RunStatus Status => RunStatus.From(_row.Chip);

    /// <summary>The badge's word, as its accessible name.</summary>
    public string StatusName => Status.Word;

    /// <summary>What the instance is about: the subject's label, or its key when it has none.</summary>
    public string Subject => _row.Subject.Label.Length > 0 ? _row.Subject.Label : _row.Subject.Key;

    /// <summary>The recipe's id, as served.</summary>
    public string Recipe => _row.Recipe.Id;

    /// <summary>The workflow's name as a person reads it: the served display name, else the id made readable.</summary>
    public string Workflow => WorkflowNames.Of(_row.Recipe.Id, _row.Recipe.DisplayName);

    /// <summary>The stage tag ("mitigate r1/3 ⇄ verify").</summary>
    public string Stage => StageTag.For(_row);

    /// <summary>The served "now" sentence, unchanged.</summary>
    public string Now => _row.Now;

    /// <summary>How long the instance has been in its state ("1h", "12m").</summary>
    public string Age => Labels.RowAge(_clock.GetUtcNow() - _row.Since);

    /// <summary>The engine badge ("▲ escalated: …"); empty without one.</summary>
    public string Badge => _row.EngineOwned is { } owned ? Labels.EngineBadge(owned.Kind, owned.Reason) : string.Empty;

    /// <summary>True when the row carries an engine badge.</summary>
    public bool HasBadge => Badge.Length > 0;

    /// <summary>The leading glyph: a triangle for an incident or an engine badge, else the plain one.</summary>
    public string Glyph => HasBadge || _row.Recipe.Urgency == RecipeUrgency.Incident ? "▲" : "◐";

    /// <summary>True when the row offers the desk answer.</summary>
    public bool CanAnswer { get; }

    /// <summary>True when the row waits on a desk-task step: its answer form is the Needs you inbox's, not the row sheet.</summary>
    internal bool AtDesk { get; }

#pragma warning disable CA1822, MA0041 // a fixed sentence is a property because the view binds it
    /// <summary>The answer button's words.</summary>
    public string AnswerText => Labels.Answer;
#pragma warning restore CA1822, MA0041

    /// <summary>The answer button's accessible name: the verb and the address.</summary>
    public string AnswerName => Labels.AnswerName(Subject);

    /// <summary>The "Answer in the child" control of a row whose answer a child decides; null for any other row.</summary>
    public ChildAnswerViewModel? ChildAnswer { get; }

    /// <summary>The lock button for a protected answer; null for none.</summary>
    public ProtectedActionViewModel? Protected { get; }

    /// <summary>The instance's address, as served: the row's id and its selection key.</summary>
    public string Address => _row.Address;

    /// <summary>The instance's own id (WFI-…), as served.</summary>
    public string InstanceId => _row.Id;

    /// <summary>The item the instance belongs to, as served; null for none.</summary>
    public string? ItemId => _row.ItemId;

    /// <summary>The name screen readers hear: the subject (the visible title), the workflow and, for an incident, the word "incident" (the glyph is not spoken).</summary>
    public string AccessibleName => Labels.StripRowName(Workflow, Subject) + (_row.Recipe.Urgency == RecipeUrgency.Incident ? ", " + Labels.Incident : "");

    /// <summary>The title button's accessible name: the visible title first, then the workflow.</summary>
    public string OpenName => Labels.StripRowName(Workflow, Subject);

    /// <summary>Where Enter and a click go: the instance in the Workflows frame.</summary>
    public DeepLink Link => new(DeepLinkKind.Workflow, _row.Address);

    /// <summary>True while the views are stalled: the row stays, marked stale.</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>True for the row the keys act on.</summary>
    public bool IsSelected { get; private set => Set(ref field, value); }

    /// <summary>Everything the row draws (the age is read from the clock, from the served since): two rows with the same signature look the same.</summary>
    internal string Signature => string.Join('|', Address, _row.Since.UtcTicks, Chip, Subject, Workflow, Stage, Now, Badge, Glyph, CanAnswer, Protected?.Cli, ChildAnswer?.Target, ChildAnswer?.Intent, ItemId);

    /// <summary>Reads the age anew.</summary>
    internal void Refresh() => OnChanged(nameof(Age));

    /// <summary>Sets what the strip knows of the row.</summary>
    /// <param name="stale">The views are stalled.</param>
    /// <param name="selected">The keys act on this row.</param>
    internal void SetContext(bool stale, bool selected) => (IsStale, IsSelected) = (stale, selected);
}
