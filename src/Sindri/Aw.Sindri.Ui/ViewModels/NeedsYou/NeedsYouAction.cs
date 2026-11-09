namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// One action of a row as served: a catalog id and the catalog's readiness for it. A decision's options each make one
/// (<see cref="OptionId"/> is the option sent, <see cref="Label"/> its served label).
/// </summary>
/// <param name="CatalogId">The catalog id (Decide, RunService, PauseService, ApproveProfile).</param>
/// <param name="Label">What the control says.</param>
/// <param name="Readiness">Whether the app can run it now.</param>
/// <param name="Cli">The served command, or empty.</param>
/// <param name="RejectLike">True for the option that turns the request down (X picks it).</param>
internal sealed record NeedsYouAction(string CatalogId, string Label, ActionReadiness Readiness, string Cli, bool RejectLike)
{
    /// <summary>False when the catalog the action was read against was empty (not read yet).</summary>
    internal bool CatalogRead { get; init; } = true;

    /// <summary>True for the option the decision serves as recommended (A runs it).</summary>
    internal bool Recommended { get; init; }

    /// <summary>For a decision option: the option's id.</summary>
    internal string OptionId { get; init; } = "";

    /// <summary>When <see cref="Cli"/> still has a placeholder the row's subject could not fill: the words that say which ("fill in name"); empty otherwise. Kept apart from the command so a copy takes the command alone.</summary>
    internal string FillInHint { get; init; } = "";

    /// <summary>True when the app can run it (the catalog says ready).</summary>
    internal bool IsReady => Readiness == ActionReadiness.Ready;
}
