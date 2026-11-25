namespace Aw.Meta.Core.Config;

/// <summary>Where `aw-meta report-defect` files toolkit defects and how many new issues it may file a day; every field is optional.</summary>
/// <param name="Repo">The GitHub repository (owner/name) the issues go to.</param>
/// <param name="DailyCap">The most new issues filed per UTC day; a seen-again defect only gets a comment and is not counted.</param>
public sealed record DefectsConfig(string Repo = "jthorare/agentic-workflow", int DailyCap = 3);
