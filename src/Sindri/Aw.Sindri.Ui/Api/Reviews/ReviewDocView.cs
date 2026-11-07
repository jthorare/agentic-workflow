namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The claimed design doc and its item tree.</summary>
internal sealed record ReviewDocView(string Id, string Title, string Path, IReadOnlyList<ReviewDocItemView> Items);
