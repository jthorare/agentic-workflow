using System.Collections;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.ViewModels.Home;

/// <summary>One group of the Home list: its heading, its row count and its rows in ranked order (home spec §7.1).</summary>
/// <param name="heading">The heading, in sentence case.</param>
/// <param name="rows">The rows the group shows.</param>
internal sealed class HomeRowGroup(string heading, IReadOnlyList<HomeRowItem> rows) : ObservableObject, IRowGroupHeading
{
    /// <inheritdoc />
    public string Heading { get; } = heading;

    /// <inheritdoc />
    public string? Summary => null;

    /// <inheritdoc />
    public bool IsCollapsed
    {
        get;
        set => Set(ref field, value);
    }

    /// <inheritdoc />
    public IEnumerable Items => Rows;

    /// <summary>The rows the group shows.</summary>
    public IReadOnlyList<HomeRowItem> Rows { get; } = rows;
}
