using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>A group whose rows, heading, summary and fold state change after the list shows it.</summary>
internal sealed class LiveGroup(string heading, IEnumerable<LiveRow>? rows = null) : IRowGroupHeading
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LiveRow> Rows { get; } = new(rows ?? []);

    public IEnumerable Items => Rows;

    public string Heading
    {
        get;
        set
        {
            field = value;
            Raise(nameof(Heading));
        }
    } = heading;

    public string? Summary
    {
        get;
        set
        {
            field = value;
            Raise(nameof(Summary));
        }
    }

    public bool IsCollapsed
    {
        get;
        set
        {
            field = value;
            Raise(nameof(IsCollapsed));
        }
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
