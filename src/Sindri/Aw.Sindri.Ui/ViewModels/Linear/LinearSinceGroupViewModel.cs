namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One fixed group of the since-you-looked card: a heading and its rows, newest first.</summary>
/// <param name="heading">"Assigned to you", "Moved" or "Other".</param>
/// <param name="rows">The issues' rows, newest change first.</param>
internal sealed class LinearSinceGroupViewModel(string heading, IReadOnlyList<LinearSinceRowViewModel> rows)
{
    /// <summary>The group's heading.</summary>
    public string Heading { get; } = heading;

    /// <summary>How many issues the group holds, as the heading's count.</summary>
    public string Count { get; } = rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The rows, newest first.</summary>
    public IReadOnlyList<LinearSinceRowViewModel> Rows { get; } = rows;
}
