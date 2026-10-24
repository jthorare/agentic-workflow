namespace Aw.Judge.Core.PromptSort;

/// <summary>The sorter's value for each of the eleven axes: 'true' or 'false' for the nine yes/no axes, a level name for complexity and ambiguity.</summary>
/// <param name="Values">The value by axis name.</param>
public sealed record SortValues(IReadOnlyDictionary<string, string> Values)
{
    /// <summary>The complexity level: trivial, small, substantial or large.</summary>
    public string Complexity => Values[SortAxes.Complexity];

    internal bool Flag(string axis) => string.Equals(Values[axis], SortAxes.Yes, StringComparison.Ordinal);

    internal bool Is(string axis, string value) => string.Equals(Values[axis], value, StringComparison.Ordinal);
}
