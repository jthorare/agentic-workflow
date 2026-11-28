namespace Aw.Sindri.Core.Reviews;

/// <summary>What the Reviews list shows of a PR's CI and required checks, kept per head (PR review page spec §6.1, §6.2 "Cache").</summary>
/// <param name="Sha">The head commit it was decided on.</param>
/// <param name="Ci">The CI roll-up on that head.</param>
/// <param name="RequiredOpen">The open required checks; null when the PR's settings name no checks (a row then says noneNamed; a row with no summary says notRead).</param>
/// <param name="At">When it was first decided; a later write for the same head and the same CI roll-up keeps it (#4333).</param>
/// <param name="FailedAt">When the oldest failing check on this head completed (#4506), for the Projects health "red for 3 d"; null when no failing run carried a time, which reads as <paramref name="At"/>.</param>
public sealed record ReviewSummary(string Sha, ReviewCi Ci, int? RequiredOpen, DateTimeOffset At, DateTimeOffset? FailedAt = null)
{
    /// <summary>"none open", "2 open"; null when <see cref="RequiredOpen"/> is.</summary>
    public string? RequiredText => RequiredOpen is { } n ? (n == 0 ? "none open" : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{n} open")) : null;
}
