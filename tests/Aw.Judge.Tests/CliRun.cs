using System.Text.Json;

namespace Aw.Judge.Tests;

/// <summary>What one judge command line produced: the exit code and the text of stdout and stderr.</summary>
/// <param name="ExitCode">The exit code.</param>
/// <param name="Stdout">Stdout, including the trailing newline the CLI adds to non-empty output.</param>
/// <param name="Stderr">Stderr, likewise.</param>
public sealed record CliRun(int ExitCode, string Stdout, string Stderr)
{
    /// <summary>Stdout parsed as one JSON document (the hooks read it with jq).</summary>
    public JsonElement Json()
    {
        Assert.EndsWith("\n", Stdout, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(Stdout);
        return document.RootElement.Clone();
    }
}
