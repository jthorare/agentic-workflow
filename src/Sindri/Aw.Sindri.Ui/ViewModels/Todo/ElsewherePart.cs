namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>The two parts of the From elsewhere line.</summary>
internal enum ElsewherePart
{
    /// <summary>PRs waiting on the owner's review; opens Reviews.</summary>
    Reviews,

    /// <summary>Linear issues assigned to the owner; opens Work on the Linear view (Settings, Connections when Linear is not connected).</summary>
    Linear,
}
