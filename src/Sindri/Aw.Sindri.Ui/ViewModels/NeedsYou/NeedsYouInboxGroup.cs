using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A group of the inbox as the page draws it: a heading, a count and the rows, patched in place on every re-read.</summary>
internal sealed class NeedsYouInboxGroup : ObservableObject, IRowGroup
{
    internal NeedsYouInboxGroup(NeedsYouGroupKind kind, string heading)
    {
        Kind = kind;
        Heading = heading;
    }

    /// <summary>The group's kind.</summary>
    internal NeedsYouGroupKind Kind { get; }

    /// <summary>The group's heading.</summary>
    public string Heading { get; }

    /// <summary>How many rows the group holds, as the header says it.</summary>
    public string CountText => Rows.Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>The header's accessible name: the heading and the count.</summary>
    public string Name => Labels.NeedsYouGroupName(Heading, CountText);

    /// <summary>The group's rows.</summary>
    public ObservableCollection<NeedsYouRowViewModel> Rows { get; } = [];

    /// <inheritdoc />
    IEnumerable IRowGroup.Items => Rows;

    /// <summary>Makes the group hold these rows, in order, touching only what differs.</summary>
    /// <param name="rows">The rows.</param>
    internal void Update(IReadOnlyList<NeedsYouRowViewModel> rows)
    {
        var before = Rows.Count;
        Patch.To(Rows, rows);
        if (before != Rows.Count)
        {
            OnChanged(nameof(CountText));
            OnChanged(nameof(Name));
        }
    }
}
