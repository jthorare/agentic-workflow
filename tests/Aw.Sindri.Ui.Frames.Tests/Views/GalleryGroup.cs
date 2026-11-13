using System.Collections;
using System.ComponentModel;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>A made-up group for the RowList tests.</summary>
internal sealed class GalleryGroup(string heading, IReadOnlyList<GalleryRow> rows) : IRowGroupHeading
{
    public string Heading { get; } = heading;

    public string? Summary => null;

    public bool IsCollapsed { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { }
        remove { }
    }

    public IEnumerable Items { get; } = rows;

    public IReadOnlyList<GalleryRow> Rows { get; } = rows;
}
