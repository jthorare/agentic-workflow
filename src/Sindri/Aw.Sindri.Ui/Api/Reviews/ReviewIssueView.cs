namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>A Linear issue chip: its key and the sindri://linear/KEY link (plain text until the Linear sheet exists).</summary>
internal sealed record ReviewIssueView(string Key, string Href);
