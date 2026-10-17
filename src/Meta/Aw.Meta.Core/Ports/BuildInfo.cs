namespace Aw.Meta.Core.Ports;

/// <summary>What the running binary was built from, for staleness checks.</summary>
/// <param name="Commit">The source commit the binary was published from; "unknown" when the build was not stamped (a dev build).</param>
/// <param name="BuiltAt">When it was published (UTC, ISO 8601); "unknown" when the build was not stamped.</param>
/// <param name="Os">The operating system it runs on ("macOS", "Linux", or the OS description).</param>
/// <param name="Rid">The runtime identifier it was built for (osx-arm64, linux-x64 ...).</param>
public sealed record BuildInfo(string Commit, string BuiltAt, string Os, string Rid);
