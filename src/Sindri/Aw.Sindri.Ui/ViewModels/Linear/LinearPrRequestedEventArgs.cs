namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>The issue's PR chip asks for the PR's review page.</summary>
/// <param name="repo">owner/name.</param>
/// <param name="number">The PR number.</param>
internal sealed class LinearPrRequestedEventArgs(string repo, int number) : EventArgs
{
    /// <summary>owner/name.</summary>
    public string Repo { get; } = repo;

    /// <summary>The PR number.</summary>
    public int Number { get; } = number;
}
