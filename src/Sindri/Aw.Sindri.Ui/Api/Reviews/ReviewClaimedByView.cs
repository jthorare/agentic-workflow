namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>Another PR that claims an item: "claimed by #2060, merged".</summary>
internal sealed record ReviewClaimedByView(int Number, bool Merged, string Text);
