namespace Aw.Sindri.Features.GetStatus;

/// <summary>The build this serve runs and the build the install points at (#3052), so the app needs no parsing of the stale-build sentence.</summary>
/// <param name="Running">The stamp this serve runs from, or null in a dev run.</param>
/// <param name="Installed">The stamp 'bin/cs/current' names, or null when there is none.</param>
/// <param name="Stale">True only when both are known and differ.</param>
public sealed record BuildStatus(string? Running, string? Installed, bool Stale);
