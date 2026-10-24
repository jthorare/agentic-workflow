using System.Globalization;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.CheckTraits;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// The arguments of 'judge trait-check' as a request, or null for a usage error: exactly one mode; --file with --edit
/// only (and --edit needs one); --path, --trait and --concurrency with --scan only; every value flag has a value; the
/// numbers are valid. The last --base, --budget-ms, --min-p and --concurrency win.
/// </summary>
internal static class TraitCheckArguments
{
    private static readonly string[] _valueFlags = ["--file", "--base", "--budget-ms", "--min-p", "--path", "--trait", "--concurrency"];

    internal static CheckTraitsRequest? Parse(IReadOnlyList<string> args, IReadOnlyCollection<ProviderName>? allowed)
    {
        if (Split(args) is not var (mode, values) || mode is not { } given || !Consistent(given, values) || Numbers(values) is not { } numbers)
        {
            return null;
        }

        return new CheckTraitsRequest(
            given,
            values["--file"],
            values["--base"].LastOrDefault(),
            values["--path"],
            values["--trait"],
            numbers.BudgetMs is { } ms ? TimeSpan.FromMilliseconds(ms) : null,
            numbers.Minimum,
            numbers.Concurrency,
            allowed?.ToArray());
    }

    private sealed record Counts(int? BudgetMs, int? Concurrency, double? Minimum);

    // --file goes with --edit (which needs one); --path, --trait and --concurrency go with --scan.
    private static bool Consistent(TraitCheckMode mode, Dictionary<string, List<string>> values)
    {
        var edit = mode == TraitCheckMode.Edit;
        var files = values["--file"].Count > 0;
        var scanFlags = values["--path"].Count > 0 || values["--trait"].Count > 0 || values["--concurrency"].Count > 0;
        return edit == files && (mode == TraitCheckMode.Scan || !scanFlags);
    }

    // The numbers, or null when one is malformed or out of range (a budget and a concurrency above 0, a probability from 0 to 1).
    private static Counts? Numbers(Dictionary<string, List<string>> values)
    {
        var budget = Whole(values["--budget-ms"], out var budgetOk);
        var concurrency = Whole(values["--concurrency"], out var concurrencyOk);
        var minimum = Fraction(values["--min-p"], out var minimumOk);
        return budgetOk && concurrencyOk && minimumOk && budget is not <= 0 && concurrency is not <= 0 && minimum is not (< 0 or > 1)
            ? new Counts(budget, concurrency, minimum)
            : null;
    }

    // The mode and the value flags' values, or null when a flag is unknown or lacks its value, or a second mode is given.
    private static (TraitCheckMode? Mode, Dictionary<string, List<string>> Values)? Split(IReadOnlyList<string> args)
    {
        TraitCheckMode? mode = null;
        var values = _valueFlags.ToDictionary(flag => flag, _ => new List<string>(), StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i++)
        {
            if (Mode(args[i]) is { } flagMode && mode is null)
            {
                mode = flagMode;
            }
            else if (Mode(args[i]) is null && values.TryGetValue(args[i], out var list) && i + 1 < args.Count)
            {
                list.Add(args[++i]);
            }
            else
            {
                return null;
            }
        }

        return (mode, values);
    }

    private static TraitCheckMode? Mode(string arg) => arg switch
    {
        "--edit" => TraitCheckMode.Edit,
        "--stop" => TraitCheckMode.Stop,
        "--scan" => TraitCheckMode.Scan,
        _ => null,
    };

    // Absent is null and fine; present must be a whole number.
    private static int? Whole(List<string> given, out bool ok)
    {
        ok = true;
        if (given.Count == 0)
        {
            return null;
        }

        ok = int.TryParse(given[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var value);
        return value;
    }

    private static double? Fraction(List<string> given, out bool ok)
    {
        ok = true;
        if (given.Count == 0)
        {
            return null;
        }

        ok = double.TryParse(given[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value);
        return value;
    }
}
