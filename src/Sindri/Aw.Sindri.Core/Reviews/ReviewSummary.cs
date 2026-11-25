namespace Aw.Sindri.Core.Reviews;

/// <summary>What the Reviews list shows of a PR's CI and required checks, kept per head (PR review page spec §6.1, §6.2 "Cache").</summary>
/// <param name="Sha">The head commit it was decided on.</param>
/// <param name="Ci">The CI roll-up on that head.</param>
/// <param name="RequiredOpen">The open required checks; null when the PR's settings name no checks (a row then says noneNamed; a row with no summary says notRead).</param>
/// <param name="At">When it was decided.</param>
public sealed record ReviewSummary(string Sha, ReviewCi Ci, int? RequiredOpen, DateTimeOffset At)
{
    /// <summary>"none open", "2 open"; null when <see cref="RequiredOpen"/> is.</summary>
    public string? RequiredText => RequiredOpen is { } n ? (n == 0 ? "none open" : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{n} open")) : null;
}
