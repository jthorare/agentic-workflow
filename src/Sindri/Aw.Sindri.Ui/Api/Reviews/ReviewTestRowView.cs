namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One test plan row and the items it verifies.</summary>
internal sealed record ReviewTestRowView(string Id, string Text, IReadOnlyList<string> Verifies, bool Ticked);
