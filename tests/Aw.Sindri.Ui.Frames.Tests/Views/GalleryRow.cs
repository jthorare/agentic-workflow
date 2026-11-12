using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>A made-up row for the RowList tests.</summary>
internal sealed record GalleryRow(string Id, string Title, IReadOnlyDictionary<string, RowFact> Facts, string Age, IReadOnlyList<RowAction> Actions) : IRowItem
{
    public string? SubLine => null;

    public string Kind => "pr";

    public RowStateClass StateClass => RowStateClass.Waiting;

    public string? StateKey => "in-review";

    public bool Unread => false;

    public RowWaitsOn? WaitsOn => new(RowWho.Me, "you");
}
