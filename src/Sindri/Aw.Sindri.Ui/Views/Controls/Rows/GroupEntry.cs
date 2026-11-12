using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The entry for a group header in a <see cref="RowList"/>'s flat run; one object per group, so the run can tell a kept header from a new one by reference.</summary>
/// <param name="group">The group.</param>
internal sealed class GroupEntry(IRowGroup group)
{
    /// <summary>The group the header shows.</summary>
    public IRowGroup Group { get; } = group;
}
