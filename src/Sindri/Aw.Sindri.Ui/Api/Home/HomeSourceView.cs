namespace Aw.Sindri.Ui.Api.Home;

/// <summary>Where a row's facts came from.</summary>
/// <param name="Name">The source's name.</param>
/// <param name="ReadAt">When Sindri last read the source.</param>
/// <param name="Connection">The connection used, or null.</param>
internal sealed record HomeSourceView(string Name, DateTimeOffset ReadAt, string? Connection);
