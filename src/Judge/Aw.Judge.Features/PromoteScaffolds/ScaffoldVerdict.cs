namespace Aw.Judge.Features.PromoteScaffolds;

/// <summary>The promotion verdict for one scaffold.</summary>
/// <param name="Scaffold">The scaffold id.</param>
/// <param name="Go">Whether its eval says it is safe to turn on.</param>
/// <param name="Reason">Why.</param>
public sealed record ScaffoldVerdict(string Scaffold, bool Go, string Reason);
