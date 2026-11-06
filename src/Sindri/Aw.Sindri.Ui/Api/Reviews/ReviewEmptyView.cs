namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The Reviews list's empty state (review page spec §6.3).</summary>
/// <param name="Text">"Nothing waiting on you and no open PRs of yours."</param>
/// <param name="Hint">Where repos are set ("Repos are set in Settings → Connections.").</param>
/// <param name="Teams">"Reviews asked of a team don't show here yet."</param>
internal sealed record ReviewEmptyView(string Text, string Hint, string Teams);
