using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>The workflows under the workflows step, one line each at its highest version (what would run), and how many of them are approved but outdated.</summary>
internal sealed class SetupRecipeListViewModel
{
    /// <summary>No workflows read.</summary>
    internal static SetupRecipeListViewModel Empty { get; } = new([]);

    /// <summary>The list over the served rows.</summary>
    /// <param name="served">Every registered workflow row, any version.</param>
    internal SetupRecipeListViewModel(IReadOnlyList<RecipeSummaryView> served)
    {
        ArgumentNullException.ThrowIfNull(served);
        // why: a group is never empty, so MaxBy finds a row
        Lines = [.. served.GroupBy(r => r.Id, StringComparer.Ordinal).Select(g => g.MaxBy(r => r.Version)!).Select(r => new SetupRecipeLineViewModel(r))];
        OutdatedCount = Lines.Count(l => l.IsOutdated);
    }

    /// <summary>The workflows, in the order served.</summary>
    public IReadOnlyList<SetupRecipeLineViewModel> Lines { get; }

    /// <summary>How many are approved but outdated.</summary>
    public int OutdatedCount { get; }

    /// <summary>True when there are lines to show.</summary>
    public bool HasLines => Lines.Count > 0;

    /// <summary>True when some are outdated: the list names the one action over them.</summary>
    public bool HasOutdated => OutdatedCount > 0;

    /// <summary>The action over the outdated ones.</summary>
#pragma warning disable CA1822, MA0041 // why: the view binds it as an instance property of the view model
    public string Heading => Labels.SetupReapproveAll;
#pragma warning restore CA1822, MA0041
}
