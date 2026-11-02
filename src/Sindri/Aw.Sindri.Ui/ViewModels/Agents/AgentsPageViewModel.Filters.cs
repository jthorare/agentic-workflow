using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The Agents page's filters (<see cref="AgentsPageViewModel"/>, continued; Q2): provider, state and repo, applied in the app over the one
/// list that was read. The head names the shown agents out of the served count while a filter is on; the shell's counts stay the API's.
/// </summary>
internal sealed partial class AgentsPageViewModel
{
    private static readonly string[] _states = ["running", "asking", "idle", "unknown"];

    /// <summary>The provider choices: all, then each provider the list has shown.</summary>
    public ObservableCollection<string> ProviderChoices { get; } = [Labels.AllProviders];

    /// <summary>The state choices: all, then the four states in the words the row chips use.</summary>
    public ObservableCollection<string> StateChoices { get; } = [Labels.AllStates, .. _states.Select(StateWord)];

    /// <summary>The repo choices: all, then each repo the list has shown.</summary>
    public ObservableCollection<string> RepoChoices { get; } = [Labels.AllRepos];

    /// <summary>The provider label the list is narrowed to; null for all.</summary>
    public string? ProviderFilter { get; private set => Set(ref field, value); }

    /// <summary>The served state the list is narrowed to (running, asking, idle, unknown); null for all.</summary>
    public string? StateFilter { get; private set => Set(ref field, value); }

    /// <summary>The repo the list is narrowed to; null for all.</summary>
    public string? RepoFilter { get; private set => Set(ref field, value); }

    /// <summary>The provider filter's accessible name: its shown choice first.</summary>
    public string ProviderFilterName => Labels.FilterName(ProviderFilter ?? Labels.AllProviders, "provider");

    /// <summary>The state filter's accessible name: its shown choice first.</summary>
    public string StateFilterName => Labels.FilterName(StateFilter is null ? Labels.AllStates : StateWord(StateFilter), "status");

    /// <summary>The repo filter's accessible name: its shown choice first.</summary>
    public string RepoFilterName => Labels.FilterName(RepoFilter ?? Labels.AllRepos, "repo");

    /// <summary>Narrows the list to one provider (a choice), or to all.</summary>
    /// <param name="choice">A provider label, or the "all" choice, or null.</param>
    internal void FilterProvider(string? choice)
    {
        ProviderFilter = Chosen(choice, Labels.AllProviders);
        OnChanged(nameof(ProviderFilterName));
        Redraw();
    }

    /// <summary>Narrows the list to one state, or to all.</summary>
    /// <param name="choice">A chip word (Working), a served state, or the "all" choice, or null.</param>
    internal void FilterState(string? choice)
    {
        StateFilter = Chosen(choice, Labels.AllStates) is { } chosen ? ServedState(chosen) : null;
        OnChanged(nameof(StateFilterName));
        Redraw();
    }

    /// <summary>Narrows the list to one repo, or to all.</summary>
    /// <param name="choice">A repo, or the "all" choice, or null.</param>
    internal void FilterRepo(string? choice)
    {
        RepoFilter = Chosen(choice, Labels.AllRepos);
        OnChanged(nameof(RepoFilterName));
        Redraw();
    }

    private static string StateWord(string state) => SessionChips.For(state).Word;

    // The combo shows the chip word; the list is matched on the served state. A word no state has (or a served state itself) stands for itself.
    private static string ServedState(string choice) =>
        _states.FirstOrDefault(s => string.Equals(StateWord(s), choice, StringComparison.Ordinal)) ?? choice;

    private static string? Chosen(string? choice, string all) => string.IsNullOrEmpty(choice) || string.Equals(choice, all, StringComparison.Ordinal) ? null : choice;

    private bool Matches(SessionRowView row) =>
        (ProviderFilter is null || string.Equals(row.ProviderLabel, ProviderFilter, StringComparison.Ordinal))
        && (StateFilter is null || string.Equals(row.State, StateFilter, StringComparison.Ordinal))
        && (RepoFilter is null || string.Equals(row.Repo, RepoFilter, StringComparison.Ordinal));

    private void Redraw()
    {
        if (_list is { } list)
        {
            Apply(WalkKeys(), list);
        }
    }

    // The choices follow what the list has shown; a filter that is set stays a choice even when nothing matches it any more.
    private void ReplaceChoices(SessionList list)
    {
        var all = list.Sessions.Concat(list.RecentlyEnded.Select(e => e.Session)).ToList();
        Replace(ProviderChoices, Labels.AllProviders, [.. all.Select(s => s.ProviderLabel), .. Extra(ProviderFilter)], sorted: true);
        Replace(RepoChoices, Labels.AllRepos, [.. all.Select(s => s.Repo).OfType<string>(), .. Extra(RepoFilter)], sorted: true);
        // The states keep their order (Working first, as the rows rank them); a state this build doesn't know follows.
        Replace(StateChoices, Labels.AllStates, [.. _states.Concat(all.Select(s => s.State)).Concat(Extra(StateFilter)).Select(StateWord)], sorted: false);
    }

    private static string[] Extra(string? value) => value is null ? [] : [value];

    private static void Replace(ObservableCollection<string> choices, string all, IEnumerable<string> values, bool sorted)
    {
        var target = new List<string> { all };
        var wanted = values.Where(v => v.Length > 0).Distinct(StringComparer.Ordinal);
        target.AddRange(sorted ? wanted.Order(StringComparer.Ordinal) : wanted);
        if (choices.SequenceEqual(target, StringComparer.Ordinal))
        {
            return;   // the control keeps its selection unless the choices themselves changed
        }

        // Edit in place: clearing the collection would drop the combo box's selection (and so clear the filter it stands for).
        foreach (var gone in choices.Where(c => !target.Contains(c, StringComparer.Ordinal)).ToList())
        {
            _ = choices.Remove(gone);
        }

        for (var i = 0; i < target.Count; i++)
        {
            if (i < choices.Count && string.Equals(choices[i], target[i], StringComparison.Ordinal))
            {
                continue;
            }

            var at = choices.IndexOf(target[i]);
            if (at >= 0)
            {
                choices.Move(at, i);
            }
            else
            {
                choices.Insert(i, target[i]);
            }
        }
    }
}
