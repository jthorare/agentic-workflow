using System.Globalization;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the list's rows, their order and groups, and the columns they keep at the list's width (#3683).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>The list's columns at its width.</summary>
    public ColumnPlan Columns { get; private set; } = ColumnPlan.Wide;

    /// <summary>The list was laid out at this width: rows drop or keep their columns.</summary>
    /// <param name="width">The list's width.</param>
    internal void SetListWidth(double width)
    {
        Columns = ColumnPlan.For(width);
        OnChanged(nameof(Columns));
        foreach (var row in Rows)
        {
            row.Columns = Columns;
        }
    }

    // "Needs a look" first (last run failed, or paused with a schedule), then by latest activity. The first row of each group carries the group's header, and a name two workflows share shows the short id.
    private static List<WorkflowRowViewModel> Sorted(List<WorkflowRowViewModel> rows)
    {
        var ordered = rows.OrderByDescending(r => r.NeedsLook).ThenByDescending(r => r.Activity).ToList();
        var names = ordered.GroupBy(r => r.NameText, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in ordered)
        {
            row.ShowsId = names.Contains(row.NameText);
        }

        var look = ordered.Count(r => r.NeedsLook);
        if (look > 0)
        {
            ordered[0].GroupHeader = "Needs a look \u00b7 " + look.ToString(CultureInfo.InvariantCulture);
        }

        if (look < ordered.Count)
        {
            ordered[look].GroupHeader = "Everything else \u00b7 " + (ordered.Count - look).ToString(CultureInfo.InvariantCulture);
        }

        return ordered;
    }

    private void ShowList(WorkflowList list)
    {
        (IsReading, IsUnreadable, IsFailed, FailedText) = (false, false, false, string.Empty);
        var rows = Sorted([.. list.Workflows.Select(w => new WorkflowRowViewModel(w, _clock))]);
        foreach (var row in rows)
        {
            row.Columns = Columns;
        }

        Patch.To(Rows, rows);
        (HasHead, Head) = rows.Count == 0 ? (false, string.Empty) : (true, Labels.WfHead(rows.Count, rows.Count(r => r.StateIsOk), rows.Count(r => r.IsPaused)));
        (IsEmpty, EmptyText) = rows.Count == 0 ? (true, Labels.WfNone) : (false, string.Empty);
        OnChanged(nameof(ShowsEmpty));
        OnChanged(nameof(ShowsHeaderNew));
        NotifyCreate();   // a short id the list now holds is taken
        NotifyStateButtons();
    }
}
