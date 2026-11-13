namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Natural string order: a run of digits compares as a number (Area 2 before Area 10), the rest ignores case; ties fall back to plain ordinal so the order is total and stable.</summary>
internal sealed class NaturalOrder : IComparer<string>
{
    /// <summary>The one instance.</summary>
    internal static readonly NaturalOrder Instance = new();

    private NaturalOrder()
    {
    }

    /// <inheritdoc />
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null || y is null)
        {
            return x is null ? -1 : 1;
        }

        var i = 0;
        var j = 0;
        while (i < x.Length && j < y.Length)
        {
            var step = char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]) ? Numbers(x, y, ref i, ref j) : CompareChar(x[i++], y[j++]);
            if (step != 0)
            {
                return step;
            }
        }

        var rest = (x.Length - i).CompareTo(y.Length - j);
        return rest != 0 ? rest : string.CompareOrdinal(x, y);
    }

    private static int CompareChar(char a, char b) => char.ToUpperInvariant(a).CompareTo(char.ToUpperInvariant(b));

    // Compares the digit runs at i and j as numbers (leading zeros skipped, then length, then digits) and moves both past them.
    private static int Numbers(string x, string y, ref int i, ref int j)
    {
        var xe = i;
        while (xe < x.Length && char.IsAsciiDigit(x[xe]))
        {
            xe++;
        }

        var ye = j;
        while (ye < y.Length && char.IsAsciiDigit(y[ye]))
        {
            ye++;
        }

        var a = x.AsSpan(i, xe - i).TrimStart('0');
        var b = y.AsSpan(j, ye - j).TrimStart('0');
        i = xe;
        j = ye;
        return a.Length != b.Length ? a.Length.CompareTo(b.Length) : a.CompareTo(b, StringComparison.Ordinal);
    }
}
