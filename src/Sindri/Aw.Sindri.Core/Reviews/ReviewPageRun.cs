namespace Aw.Sindri.Core.Reviews;

/// <summary>One CI run on the head.</summary>
/// <param name="Name">The check's name.</param>
/// <param name="App">The app that reported it.</param>
/// <param name="Status">completed or in_progress.</param>
/// <param name="Conclusion">The conclusion; null while running.</param>
/// <param name="Sha7">The commit it ran on.</param>
/// <param name="Url">Where to open it.</param>
public sealed record ReviewPageRun(
    string Name,
    string App,
    string Status,
    string? Conclusion,
    string Sha7,
    string Url);
