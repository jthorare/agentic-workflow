namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One risky path the PR touches and why it is risky.</summary>
internal sealed record ReviewRiskHitView(string Path, string Why, string Source);
