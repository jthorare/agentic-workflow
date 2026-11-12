using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The fold keys of <see cref="RowList"/> (#2978): Left folds the cursor's group, Right unfolds it.</summary>
internal sealed partial class RowList
{
    /// <inheritdoc />
    public bool TryFold(int index, bool collapse)
    {
        var header = -1;
        foreach (var state in _run.States)
        {
            var at = _run.HeaderIndex(state);
            if (index < at || index > at + state.Visible)
            {
                continue;
            }

            header = at;
            if (state.Group is not IRowGroupHeading heading)
            {
                return false;
            }

            if (heading.IsCollapsed != collapse)
            {
                heading.IsCollapsed = collapse;
            }

            if (collapse && index != header)
            {
                RowKeys.FocusRow(this, header);   // the row left with the fold: the cursor goes to the header, where Right unfolds it
            }

            return true;
        }

        return header >= 0;
    }
}
