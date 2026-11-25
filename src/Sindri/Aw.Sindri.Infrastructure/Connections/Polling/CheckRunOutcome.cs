namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>How the check runs on an open pull request's head ended (#3201): what a github.pr.checks event carries beyond the pull request itself.</summary>
/// <param name="Conclusion">'red' or 'green'.</param>
/// <param name="Failing">The names of the failing checks (empty when green).</param>
/// <param name="At">When the last check finished.</param>
/// <param name="RunId">The highest check run id on the head (a re-run gets a new one).</param>
internal sealed record CheckRunOutcome(string Conclusion, IReadOnlyList<string> Failing, DateTimeOffset At, long RunId);
