namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>A PR's head: the commit and its branch.</summary>
internal sealed record ReviewHeadView(string Sha, string Sha7, string Branch);
