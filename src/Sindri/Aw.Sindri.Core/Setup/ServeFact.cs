namespace Aw.Sindri.Core.Setup;

/// <summary>Whether the running Sindri is the installed build (first-run spec §3.2).</summary>
/// <param name="Current">True when serve runs the installed build, or when there is no stamp to compare (a dev run, a test).</param>
public sealed record ServeFact(bool Current);
