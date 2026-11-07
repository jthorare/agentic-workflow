using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>Builds the stored PR records the Reviews list tests read.</summary>
internal static class ReviewPulls
{
    internal const string Head = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    internal const string Old = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    internal static readonly DateTimeOffset Day = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    internal static ReviewPull Pull(string repo = "acme/api", int number = 1, string author = "sam-dev", string[]? requested = null, ReviewedAt[]? reviews = null, string? head = Head,
        bool read = true, ReviewClaim[]? claims = null, int updatedDays = 0, bool draft = false, string? url = null) =>
        new(repo, number, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Title {number}"), author, draft, Day.AddDays(-5), Day.AddDays(updatedDays), head, requested ?? [], reviews ?? [], read, claims ?? [], url);
}
