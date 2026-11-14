using System.ComponentModel;
using System.Globalization;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>A row that changes after the list shows it: its title, age and facts raise PropertyChanged, the way Home's rows do.</summary>
internal sealed class LiveRow(string key, string title) : IRowItem
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; } = key;

    public string Id => Key;

    public string Title
    {
        get;
        set => Set(ref field, value, nameof(Title));
    } = title;

    public string Age
    {
        get;
        set => Set(ref field, value, nameof(Age));
    } = "1m";

    public IReadOnlyDictionary<string, RowFact> Facts
    {
        get;
        set => Set(ref field, value, nameof(Facts));
    } = new Dictionary<string, RowFact>(StringComparer.Ordinal);

    public string? SubLine => null;

    public string Kind => "pr";

    public RowStateClass StateClass => RowStateClass.Waiting;

    public string? StateKey => "in-review";

    public bool Unread => false;

    public RowWaitsOn? WaitsOn => null;

    public IReadOnlyList<RowAction> Actions => [];

    /// <summary>Row number i with key prefix+i and title label+i.</summary>
    public static LiveRow At(int i, string prefix = "k", string label = "Row ") => new(prefix + i.ToString(CultureInfo.InvariantCulture), label + i.ToString(CultureInfo.InvariantCulture));

    private void Set<T>(ref T field, T value, string name)
    {
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
