using System.ComponentModel;
using Aw.Sindri.Ui.Api.Home;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.ViewModels.Home;

/// <summary>A Home row as the row list draws it (home spec §7.1 "Row"): a plain map of the API's row. A fresh read makes a new object with the same <see cref="Key"/>.</summary>
internal sealed class HomeRowItem : IRowItem
{
    /// <summary>The fact column keys, in the order the row shows them.</summary>
    internal const string Why = "why";

    /// <inheritdoc cref="Why" />
    internal const string Object = "object";

    /// <inheritdoc cref="Why" />
    internal const string From = "from";

    private HomeRowItem(HomeRowView row)
    {
        Row = row;
    }

    // A row never changes in place: a read swaps in a new object with the same key.
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { }
        remove { }
    }

    /// <summary>The API's row.</summary>
    internal HomeRowView Row { get; }

    /// <inheritdoc />
    public string Key => Row.Id;

    /// <inheritdoc />
    public string Id { get; private init; } = string.Empty;

    /// <inheritdoc />
    public string Title => Row.Title;

    /// <inheritdoc />
    public string? SubLine => null;

    /// <inheritdoc />
    public string Kind { get; private init; } = string.Empty;

    /// <inheritdoc />
    public RowStateClass StateClass { get; private init; }

    /// <inheritdoc />
    public string? StateKey { get; private init; }

    /// <inheritdoc />
    public bool Unread { get; private init; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, RowFact> Facts { get; private init; } = new Dictionary<string, RowFact>(StringComparer.Ordinal);

    /// <inheritdoc />
    public string Age { get; private init; } = string.Empty;

    /// <inheritdoc />
    public RowWaitsOn? WaitsOn { get; private init; }

    /// <inheritdoc />
    public IReadOnlyList<RowAction> Actions => [];

    /// <summary>Maps an API row.</summary>
    /// <param name="row">The row as served.</param>
    /// <param name="now">The time the age counts to.</param>
    /// <returns>The row to draw.</returns>
    internal static HomeRowItem Of(HomeRowView row, DateTimeOffset now) => new(row) { Age = now.ToString("O", System.Globalization.CultureInfo.InvariantCulture) };   // the stub of the red commit: no map yet
}
