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
        throw new NotSupportedException();
    }
}
