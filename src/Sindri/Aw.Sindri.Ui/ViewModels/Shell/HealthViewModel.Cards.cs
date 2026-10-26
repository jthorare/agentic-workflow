using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The System overview's Agents, Views and Code index cards and what they add to the headline (<see cref="HealthViewModel"/>, continued).</summary>
internal sealed partial class HealthViewModel
{
    private void ShowAgentsCard() => AgentsRows = Sessions is { } s
        ? [new(Labels.AgentsWorking, Count(s.Running ?? s.Live)), new(Labels.AgentsAsking, Count(s.Asking)), new(Labels.AgentsProblems, Count(s.Problems))]
        : [];

    private void ShowViewsCard()
    {
        var views = _serviceList?.AsOf;
        ViewsWord = views switch
        {
            null => Labels.ViewsUnknown,
            { Stalled: true } => Labels.ViewsStalled,
            { Rebuilding: true } => Labels.ViewsCardRebuilding,
            _ => Labels.ViewsUpToDate,
        };
        ViewsRows = views is null ? [] : ViewsCells(views);
        ViewsFix = views is { } v && (v.Stalled || v.Rebuilding) ? Catalog.Rebuild : null;
    }

    private CodeIndexCardViewModel WatchCodeIndex(CodeIndexCardViewModel card)
    {
        card.PropertyChanged += CodeIndexChanged;
        return card;
    }

    // What the cards say needs a look that the server's one health word does not: the Code index; empty when it is fine.
    private string NeedsLook()
    {
        List<string> parts = [];
        if (string.Equals(CodeIndex.Word, Labels.CodeIndexNeedsLook, StringComparison.Ordinal))
        {
            parts.Add(Labels.CodeIndexLookName);
        }

        return string.Join(", ", parts);
    }

    // The Code index card read again: the headline follows it.
    private void CodeIndexChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(CodeIndexCardViewModel.Word), StringComparison.Ordinal))
        {
            lock (_gate)
            {
                ShowSystemState();
            }
        }
    }

    private static string Count(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static List<HealthCell> ViewsCells(AsOfView views)
    {
        List<HealthCell> cells = [new(Labels.ViewsReadUpTo, Labels.ViewsProgress(views.Cursor, views.LedgerHead))];
        if (views.ProjectedAt is { } at)
        {
            cells.Add(new(Labels.ViewsUpdated, StatusText.Time(at)));
        }

        return cells;
    }
}
