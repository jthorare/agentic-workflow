using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.Scrub;

/// <summary>The pre-commit hook Sindri installs (cutover spec §6.2 item 3): its body, and whether a file already there is ours.</summary>
internal static class PreCommitHook
{
    /// <summary>The marker line of the current hook.</summary>
    internal const string Marker = "# sindri-pre-commit v3";

    private const string V2Marker = "# sindri-pre-commit v2";
    private const string V1Marker = "# sindri-scrub-pre-commit v1";

    /// <summary>The hook script: the marker, the binary path baked in, the scan, and a refusal that names ./setup.sh when the binary is missing. No shape line.</summary>
    /// <param name="bin">The path baked into the hook (the stable ~/.local/bin/sindri, never a stamped build directory).</param>
    /// <returns>The file's text.</returns>
    internal static string Body(string bin)
    {
        ArgumentNullException.ThrowIfNull(bin);
        var quoted = "'" + bin.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
        return string.Join('\n',
            "#!/bin/sh",
            Marker,
            $"SINDRI={quoted}",
            "if [ ! -x \"$SINDRI\" ]; then",
            "  echo \"sindri-scrub: $SINDRI not found; run ./setup.sh in the agentic-workflow checkout to install it\" >&2",
            "  exit 1",
            "fi",
            "\"$SINDRI\" scrub --staged || exit 1",
            string.Empty);
    }

    /// <summary>Classifies an existing pre-commit file by whole-line markers; a substring does not count.</summary>
    /// <param name="existing">The file's text, or null when there is no file.</param>
    /// <returns>The kind.</returns>
    internal static HookKind Classify(string? existing)
    {
        if (existing is null)
        {
            return HookKind.None;
        }

        var lines = existing.Split('\n').Select(static l => l.TrimEnd('\r')).ToHashSet(StringComparer.Ordinal);
        return lines.Contains(Marker) ? HookKind.V3
            : lines.Contains(V2Marker) ? HookKind.V2
            : lines.Contains(V1Marker) ? HookKind.V1
            : HookKind.Foreign;
    }
}
