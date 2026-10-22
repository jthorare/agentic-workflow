using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>
/// One item row (UI spec §4.5): id, title, repo, the state as a word, step, since, and the row's primary action from the
/// catalog. Every field is the API's; the row decides nothing. A refresh updates the row in place (<see cref="Update"/>),
/// so the controls built for it, and the focus on them, stay (§3.3).
/// </summary>
internal sealed class ItemRowViewModel : ObservableObject
{
    internal ItemRowViewModel(ItemSummaryView item, string group, ControlViewModel? primaryAction, DateTimeOffset now)
    {
        Id = item.Id;
        (Group, Title, Repo, StateWord, Step, Age, Name, PrimaryAction, Badge) = Fields(item, group, primaryAction, now);
    }

    /// <summary>The item's id ('ITM-…').</summary>
    public string Id { get; }

    /// <summary>The API group the row came in.</summary>
    public string Group { get; private set; }

    /// <summary>The title as served (plain text).</summary>
    public string Title { get; private set => Set(ref field, value); }

    /// <summary>The repo, or empty.</summary>
    public string Repo { get; private set => Set(ref field, value); }

    /// <summary>The item state as a word.</summary>
    public string StateWord { get; private set => Set(ref field, value); }

    /// <summary>The engine step, or empty before S2.</summary>
    public string Step { get; private set { Set(ref field, value); OnChanged(nameof(HasStep)); OnChanged(nameof(Stage)); OnChanged(nameof(StageIsRaw)); } }

    /// <summary>The stage in plain words, or "Not started".</summary>
    public string Stage => Labels.StageName(Step).Name;

    /// <summary>True when the stage shows the served step because the app has no plain name for it.</summary>
    public bool StageIsRaw => Labels.StageName(Step).Raw;

    /// <summary>True when the API names a step.</summary>
    public bool HasStep => Step.Length > 0;

    /// <summary>How long since the item entered its state ("12m").</summary>
    public string Age { get; private set => Set(ref field, value); }

    /// <summary>The row's accessible name: its id, title and state.</summary>
    public string Name { get; private set => Set(ref field, value); }

    /// <summary>The row's title button's accessible name: "Open" and the id.</summary>
    public string OpenName => "Open " + Id;

    /// <summary>The API's primaryAction as the catalog's control (pending rows drawn disabled), or null.</summary>
    public ControlViewModel? PrimaryAction { get; private set { Set(ref field, value); OnChanged(nameof(HasPrimaryAction)); } }

    /// <summary>True when the row has a primary action to show.</summary>
    public bool HasPrimaryAction => PrimaryAction is not null;

    /// <summary>
    /// "parked" for a parked item (UI spec §4.5, owner 2026-10-12: parked items sit in Waiting on you, API §3.6), from
    /// the item's state; empty otherwise.
    /// </summary>
    public string Badge { get; private set { Set(ref field, value); OnChanged(nameof(HasBadge)); } }

    /// <summary>True when the row shows a badge.</summary>
    public bool HasBadge => Badge.Length > 0;

    /// <summary>True while the row is the selected one (J/K, a click, focus).</summary>
    public bool IsSelected { get; set => Set(ref field, value); }

    /// <summary>Shows the item's current fields. The primary action is kept when it is the same action, so its state survives.</summary>
    /// <param name="item">The API's row.</param>
    /// <param name="group">The group it came in.</param>
    /// <param name="primaryAction">A control for the item's primary action, or null.</param>
    /// <param name="now">The time the age counts to.</param>
    internal void Update(ItemSummaryView item, string group, ControlViewModel? primaryAction, DateTimeOffset now)
    {
        (Group, Title, Repo, StateWord, Step, Age, Name, var action, Badge) = Fields(item, group, primaryAction, now);
        PrimaryAction = PrimaryAction is { } own && action is { } next && string.Equals(own.Action, next.Action, StringComparison.Ordinal) ? own : action;
    }

    private static (string Group, string Title, string Repo, string StateWord, string Step, string Age, string Name, ControlViewModel? Action, string Badge) Fields(
        ItemSummaryView item, string group, ControlViewModel? primaryAction, DateTimeOffset now)
    {
        var state = Labels.ItemState(item.State);
        return (group, item.Title, item.Repo ?? string.Empty, state, item.Step ?? string.Empty, Labels.Age(now - item.Since),
            item.Id + " " + item.Title + ", " + state, primaryAction, item.State == "parked" ? Labels.ParkedBadge : string.Empty);
    }
}
