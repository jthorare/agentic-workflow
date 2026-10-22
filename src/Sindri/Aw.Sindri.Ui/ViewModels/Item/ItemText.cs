namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>The queue's and the item sheet's own copy (<see cref="Labels"/>) as the views bind it (§12.4); see Shell.ShellText.</summary>
internal sealed class ItemText
{
    /// <summary>The one instance.</summary>
    internal static ItemText Instance { get; } = new();

    /// <summary>Create item, the button's label (the catalog's label for CreateItem).</summary>
    public string CreateItem { get; } = Labels.Action("CreateItem");

    /// <summary><see cref="Labels.ItemIdLabel"/>.</summary>
    public string ItemIdLabel { get; } = Labels.ItemIdLabel;

    /// <summary><see cref="Labels.CopyItemId"/>.</summary>
    public string CopyItemId { get; } = Labels.CopyItemId;

    /// <summary>The callout's link to Needs you.</summary>
    public string OpenNeedsYou { get; } = Labels.OpenNeedsYou;

    /// <summary>The repo chip's accessible name.</summary>
    public string RepoChip { get; } = Labels.RepoChipName;

    /// <summary><see cref="Labels.ColumnTitle"/>.</summary>
    public string ColumnTitle { get; } = Labels.ColumnTitle;

    /// <summary><see cref="Labels.ColumnRepo"/>.</summary>
    public string ColumnRepo { get; } = Labels.ColumnRepo;

    /// <summary><see cref="Labels.ColumnState"/>.</summary>
    public string ColumnState { get; } = Labels.ColumnState;

    /// <summary><see cref="Labels.ColumnStep"/>.</summary>
    public string ColumnStep { get; } = Labels.ColumnStep;

    /// <summary><see cref="Labels.ColumnSince"/>.</summary>
    public string ColumnSince { get; } = Labels.ColumnSince;

    /// <summary><see cref="Labels.Filter"/>.</summary>
    public string Filter { get; } = Labels.Filter;

    /// <summary><see cref="Labels.Stale"/>.</summary>
    public string Stale { get; } = Labels.Stale;

    /// <summary><see cref="Labels.LoadMore"/>.</summary>
    public string LoadMore { get; } = Labels.LoadMore;

    /// <summary><see cref="Labels.ItemDecisions"/>.</summary>
    public string Decisions { get; } = Labels.ItemDecisions;

    /// <summary><see cref="Labels.ItemRequests"/>.</summary>
    public string Requests { get; } = Labels.ItemRequests;

    /// <summary><see cref="Labels.Timeline"/>.</summary>
    public string Timeline { get; } = Labels.Timeline;

    /// <summary><see cref="Labels.ItemActions"/>.</summary>
    public string Actions { get; } = Labels.ItemActions;

    /// <summary><see cref="Labels.Link"/>.</summary>
    public string Link { get; } = Labels.Link;

    /// <summary><see cref="Labels.CreatedBy"/>.</summary>
    public string CreatedBy { get; } = Labels.CreatedBy;

    /// <summary><see cref="Labels.WaitingOn"/>.</summary>
    public string WaitingOn { get; } = Labels.WaitingOn;

    /// <summary><see cref="Labels.Instance"/> (the instance row).</summary>
    public string InstanceLabel { get; } = Labels.Instance;

    /// <summary><see cref="Labels.SheetDetails"/>.</summary>
    public string Details { get; } = Labels.SheetDetails;

    /// <summary><see cref="Labels.SheetVersion"/>.</summary>
    public string VersionLabel { get; } = Labels.SheetVersion;

    /// <summary><see cref="Labels.Confirm"/>.</summary>
    public string Confirm { get; } = Labels.Confirm;

    /// <summary><see cref="Labels.Keep"/>.</summary>
    public string Keep { get; } = Labels.Keep;

    /// <summary><see cref="Labels.ItemTitle"/>.</summary>
    public string Title { get; } = Labels.ItemTitle;

    /// <summary><see cref="Labels.TitleRequired"/>.</summary>
    public string TitleRequired { get; } = Labels.TitleRequired;

    /// <summary><see cref="Labels.ItemRepo"/>.</summary>
    public string Repo { get; } = Labels.ItemRepo;

    /// <summary><see cref="Labels.ItemBody"/>.</summary>
    public string Body { get; } = Labels.ItemBody;

    /// <summary><see cref="Labels.Cancel"/>.</summary>
    public string Cancel { get; } = Labels.Cancel;

    /// <summary><see cref="Labels.Back"/>.</summary>
    public string Back { get; } = Labels.Back;
}
