namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One CI run on the head.</summary>
internal sealed record ReviewRunView(string Name, string App, string Status, string? Conclusion, string Sha7, string Url);
