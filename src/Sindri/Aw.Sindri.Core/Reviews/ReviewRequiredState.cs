namespace Aw.Sindri.Core.Reviews;

/// <summary>The three words a Reviews row's requiredState takes on the wire: why the Required column is blank, or that it holds a count.</summary>
public static class ReviewRequiredState
{
    /// <summary>No stored summary for the row yet: Sindri has not read the PR's checks.</summary>
    public const string NotRead = "notRead";

    /// <summary>A summary exists and the PR's settings name no required checks.</summary>
    public const string NoneNamed = "noneNamed";

    /// <summary>A summary exists and <see cref="ReviewRow.RequiredOpen"/> holds a number, 0 included.</summary>
    public const string Counted = "counted";
}
