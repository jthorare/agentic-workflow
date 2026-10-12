using Aw.Kernel.Testing;

namespace Aw.Judge.Tests;

/// <summary>
/// The golden-file check for the judge's CLI output (csharp-testing.md, Test data): stdout compared exactly with a
/// file in this project's source tree after the declared normalization, decision ids (GUIDs) and timestamps. The
/// exit code and stderr stay with each test's own assertions. AW_GOLDEN_UPDATE=1 rewrites a file for review.
/// </summary>
internal static class CliGolden
{
    private static readonly Normalization _normalization = new(Normalization.Guids, Normalization.IsoTimestamps);

    /// <summary>Checks a command's stdout against a golden file.</summary>
    /// <param name="run">The command's run.</param>
    /// <param name="relativePath">The golden file, relative to the test project, e.g. 'GetHealth/Golden/health-ok.json'.</param>
    public static void Match(CliRun run, string relativePath) =>
        Golden.Match(run.Stdout, typeof(CliGolden).Assembly, relativePath, _normalization);
}
