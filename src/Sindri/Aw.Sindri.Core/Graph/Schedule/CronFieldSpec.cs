using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Aw.Sindri.Core.Graph.Schedule;

/// <summary>One cron field's name, bounds and optional names (JAN, MON); fills the set of values a field text allows.</summary>
internal sealed record CronFieldSpec(string Name, int Min, int Max, string[]? Names, int NameBase)
{
    private string Range => Num(Min) + "-" + Num(Max);

    internal static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    public bool TryFill(string text, bool[] set, [NotNullWhen(false)] out string? error)
    {
        foreach (var item in text.Split(','))
        {
            if (item.Length == 0)
            {
                error = Name + " field '" + text + "' has an empty item; use " + Range + ", separated by single commas";
                return false;
            }

            var (rangeText, stepText) = SplitOnce(item, '/');
            var step = 1;
            if (stepText is not null && (!int.TryParse(stepText, NumberStyles.None, CultureInfo.InvariantCulture, out step) || step < 1))
            {
                error = Name + " field '" + text + "' has a bad step '" + stepText + "'; a step is a whole number of 1 or more, like */5";
                return false;
            }

            if (Bounds(rangeText, stepText is not null, text, out var from, out var to) is { } problem)
            {
                error = problem;
                return false;
            }

            for (var v = from; v <= to; v += step)
            {
                set[v] = true;
            }
        }

        error = null;
        return true;
    }

    private string? Bounds(string rangeText, bool hasStep, string text, out int from, out int to)
    {
        (from, to) = (Min, Max);
        if (rangeText == "*")
        {
            return null;
        }

        var (low, high) = SplitOnce(rangeText, '-');
        if (!TryValue(low, out from) || (high is not null && !TryValue(high, out to)))
        {
            return Name + " field '" + text + "' has a value that is not valid; use " + Range + (Names is null ? string.Empty : ", or a name like " + Names[0]);
        }

        if (high is null)
        {
            // why: "5/15" means from 5 to the top in steps, as in Vixie cron.
            to = hasStep ? Max : from;
            return null;
        }

        return from > to ? Name + " field '" + text + "' has a range that starts after it ends; write it as " + Num(to) + "-" + Num(from) : null;
    }

    private bool TryValue(string text, out int value)
    {
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            return value >= Min && value <= Max;
        }

        var index = Names is null ? -1 : Array.FindIndex(Names, n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
        value = index + NameBase;
        return index >= 0;
    }

    private static (string, string?) SplitOnce(string text, char separator)
    {
        var at = text.IndexOf(separator, StringComparison.Ordinal);
        return at < 0 ? (text, null) : (text[..at], text[(at + 1)..]);
    }
}
