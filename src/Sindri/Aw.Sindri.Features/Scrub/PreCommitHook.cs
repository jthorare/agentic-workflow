using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.Scrub;

#pragma warning disable MA0182 // until cutover unit 4.1 calls it from the installer
/// <summary>The pre-commit hook Sindri installs (cutover spec §6.2 item 3): its body, and whether a file already there is ours.</summary>
internal static class PreCommitHook
{
    /// <summary>The marker line of the current hook.</summary>
    internal const string Marker = "# sindri-pre-commit v3";

    /// <summary>The TS build's markers, recognised so the installer replaces those hooks.</summary>
    internal static readonly IReadOnlyList<string> LegacyMarkers = ["# sindri-pre-commit v2", "# sindri-scrub-pre-commit v1"];

    /// <summary>The hook script: the marker, the binary path baked in, the scan, and a refusal that names ./setup.sh when the binary is missing. No shape line.</summary>
    /// <param name="bin">The path baked into the hook (the stable ~/.local/bin/sindri, never a stamped build directory).</param>
    /// <returns>The file's text.</returns>
    internal static string Body(string bin) => throw new NotSupportedException("hook v3 is built in cutover unit 4.1");

    /// <summary>Classifies an existing pre-commit file by whole-line markers; a substring does not count.</summary>
    /// <param name="existing">The file's text, or null when there is no file.</param>
    /// <returns>The kind.</returns>
    internal static HookKind Classify(string? existing) => throw new NotSupportedException("hook v3 is built in cutover unit 4.1");
}
#pragma warning restore MA0182
