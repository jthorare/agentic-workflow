namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A map page the "Pick another" search found.</summary>
/// <param name="Key">The map surface's node key the page would point at.</param>
/// <param name="Name">What the map calls it.</param>
internal sealed record PageMatch(string Key, string Name)
{
    /// <summary>Gets the row's second line: the map surface it names ("From the map: GET /schedule/week").</summary>
    public string FromMap => PageRows.FromMap(Key);
}
