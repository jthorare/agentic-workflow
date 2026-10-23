using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// One stage cell of a Workflows row (screens §4.2): a glyph, then what the cell serves (the round, desk, child or progress). An engine
/// badge takes the glyph's place; so does the blocked dash on the state the instance waits in while a required input is missing.
/// Every part is a served field; the cell works nothing out from the recipe.
/// </summary>
internal sealed class StageCellViewModel
{
    private readonly StateCell _cell;

    /// <summary>A cell of a row.</summary>
    /// <param name="cell">The served cell.</param>
    /// <param name="isCurrent">True for the state the instance is in.</param>
    /// <param name="isBlocked">True when a required input is missing and this is the state the instance waits in.</param>
    /// <param name="names">The recipes' state names; null names the state by its id.</param>
    /// <param name="recipeId">The recipe the cell belongs to, for <paramref name="names"/>.</param>
    internal StageCellViewModel(StateCell cell, bool isCurrent, bool isBlocked, StateNames? names = null, string recipeId = "")
    {
        _cell = cell;
        StateName = names is null ? cell.State : names.Of(recipeId, cell.State);
        IsCurrent = isCurrent;
        IsBlocked = isBlocked;
        var badge = cell.Badge is { } b ? Labels.CellBadge(b.Kind, b.Reason) : string.Empty;
        Glyph = isBlocked ? Labels.BlockedMark : badge.Length > 0 ? badge : Mark(cell.Glyph);
        Detail = string.Join(' ', new[] { isBlocked ? badge : string.Empty, Parts(cell) }.Where(p => p.Length > 0));
    }

    private string StateName { get; }

    /// <summary>The state this cell is.</summary>
    public string State => _cell.State;

    /// <summary>The leading mark: the progress glyph, the engine badge, or the blocked dash.</summary>
    public string Glyph { get; }

    /// <summary>What follows the mark: the round, desk, child or progress.</summary>
    public string Detail { get; }

    /// <summary>The cell's words, the mark then the detail.</summary>
    public string Text => Detail.Length > 0 ? $"{Glyph} {Detail}" : Glyph;

    /// <summary>True when a required input is missing and this is the state the instance waits in.</summary>
    public bool IsBlocked { get; }

    /// <summary>True for the state the instance is in.</summary>
    public bool IsCurrent { get; }

    /// <summary>The name screen readers hear: the state in the recipe's words (its id when no names are known), what the cell says in words, and "current" for the state the instance is in.</summary>
    public string Name => $"{StateName}, {Words()}{(IsCurrent ? ", " + Labels.StageCurrent : string.Empty)}";

    internal static string Mark(Api.Workflow.StateGlyph glyph) => glyph switch
    {
        Api.Workflow.StateGlyph.Done => Labels.MarkDone,
        Api.Workflow.StateGlyph.Active => Labels.MarkActive,
        Api.Workflow.StateGlyph.Pending => Labels.MarkPending,
        _ => throw new ArgumentOutOfRangeException(nameof(glyph), glyph, message: null),
    };

    private static string Parts(StateCell cell) => string.Join(
        ' ',
        new[]
        {
            cell.Round is { } r ? string.Create(CultureInfo.InvariantCulture, $"r{r.N}/{r.Max}") : string.Empty,
            cell.Children is { } c ? string.Create(CultureInfo.InvariantCulture, $"{c.Done}/{c.Total}") : string.Empty,
            cell.Desk ? Labels.StageDesk : string.Empty,
            cell.Child && cell.Children is null ? "child" : string.Empty,
        }.Where(p => p.Length > 0));

    // Glyphs are never the only sign (spec §11): the same cell in words.
    private string Words()
    {
        var progress = _cell.Glyph switch
        {
            Api.Workflow.StateGlyph.Done => Labels.StageDone,
            Api.Workflow.StateGlyph.Active => Labels.StageActive,
            Api.Workflow.StateGlyph.Pending => Labels.StagePending,
            _ => Labels.StagePending,
        };
        return string.Join(", ", new[] { IsBlocked ? Labels.StageBlocked : progress, Detail }.Where(p => p.Length > 0));
    }
}
