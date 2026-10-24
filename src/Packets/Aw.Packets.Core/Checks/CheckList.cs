using System.Globalization;

namespace Aw.Packets.Core.Checks;

/// <summary>
/// Collects one rule's outcomes: its failures (at most <see cref="MaxFailures"/>, then one record counting the rest),
/// or a single pass when it found none.
/// </summary>
internal sealed class CheckList
{
    /// <summary>The most failures one rule reports in full.</summary>
    internal const int MaxFailures = 500;

    private readonly List<CheckResult> _results = [];

    internal IReadOnlyList<CheckResult> Results => _results;

    internal void Rule(string rule, string passMessage, IEnumerable<(string Subject, string Message, string Fix)> failures)
    {
        // keep the first MaxFailures in the rule's own (deterministic) order and count the rest, so a packet with
        // millions of failing pairs costs neither memory nor output
        var found = new List<CheckResult>();
        var more = 0;
        foreach (var (subject, message, fix) in failures)
        {
            if (found.Count < MaxFailures)
            {
                found.Add(new CheckResult(rule, CheckState.Fail, subject, message, fix));
            }
            else
            {
                more++;
            }
        }

        found.Sort(static (x, y) => string.CompareOrdinal(x.Subject, y.Subject) is var c and not 0 ? c : string.CompareOrdinal(x.Message, y.Message));
        if (found.Count == 0)
        {
            _results.Add(new CheckResult(rule, CheckState.Pass, "packet", passMessage, Fix: null));
            return;
        }

        _results.AddRange(found);
        if (more > 0)
        {
            _results.Add(new CheckResult(
                rule,
                CheckState.Omitted,
                "packet",
                string.Create(CultureInfo.InvariantCulture, $"… and {more} more {rule} failures, not listed"),
                "Fix the failures above and run validate again to see the rest.")
            { Omitted = more });
        }
    }

    internal void AddRange(IEnumerable<CheckResult> results) => _results.AddRange(results);

    internal void Unchecked(string rule, string reason) =>
        _results.Add(new CheckResult(rule, CheckState.Unchecked, "packet", reason, Fix: null));
}
