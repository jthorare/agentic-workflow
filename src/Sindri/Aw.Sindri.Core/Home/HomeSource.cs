namespace Aw.Sindri.Core.Home;

/// <summary>Where a row's facts came from (home spec §3.1).</summary>
/// <param name="Name">The source's name.</param>
/// <param name="ReadAt">When Sindri last read the source.</param>
/// <param name="Connection">The connection used, or null.</param>
public sealed record HomeSource(string Name, DateTimeOffset ReadAt, string? Connection);
