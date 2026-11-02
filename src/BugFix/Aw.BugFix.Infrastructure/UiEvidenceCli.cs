using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Infrastructure;

/// <summary>
/// The ui-evidence port over the ui-evidence binary (cutover spec §4.3 a): ui-evidence run &lt;script&gt; &lt;run-dir&gt; --app-build &lt;commit&gt;,
/// found by AW_UI_EVIDENCE_BIN else PATH. Exit 0 is a passing run, 2 a failed check, 4 a broken one, anything else AW-BUGFIX-017.
/// The steps and the script hash come from the run's summary.json.
/// </summary>
internal sealed class UiEvidenceCli(Func<string, string?> environment) : IUiEvidence
{
    private const string Summary = "summary.json";
    private readonly BugFixSettings _settings = BugFixSettings.Read(environment);

    public Result<UiRun> Run(UiRunRequest request)
    {
        var (binary, missing) = ToolBinary.Find(environment, "AW_UI_EVIDENCE_BIN", "ui-evidence");
        if (binary is null)
        {
            return Unavailable(missing);
        }

        var summary = Path.Combine(Directory.CreateDirectory(request.RunDirectory).FullName, Summary);
        File.Delete(summary);   // a summary left by an earlier run is never this run's
        var start = ToolBinary.Start(environment, binary, ["run", request.Script, request.RunDirectory, "--app-build", request.Commit], request.WorkingDirectory);
        var (code, ran) = Processes.ToLog(start, _settings.CheckTimeout, _settings.CheckGrace, request.Log);
        if (!ran)
        {
            return Unavailable($"ui-evidence didn't run to an exit (see {request.Log})");
        }

        if (code is not (0 or 2 or 4))
        {
            return Unavailable(string.Create(CultureInfo.InvariantCulture, $"ui-evidence exited {code} (see {request.Log})"));
        }

        return ReadSummary(summary).Map(read => new UiRun(read.Steps, read.ScriptSha256, code));
    }

    private static Result<(IReadOnlyList<RunOutcome> Steps, string ScriptSha256)> ReadSummary(string path)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is JsonObject root && root["scriptSha256"]?.GetValue<string>() is { } sha && root["steps"] is JsonArray steps)
            {
                IReadOnlyList<RunOutcome> outcomes = [.. steps.Select(static s => s?["status"]?.GetValue<string>() switch
                {
                    "passed" => RunOutcome.Passed,
                    "failed" => RunOutcome.Failed,
                    _ => RunOutcome.Broken,
                })];
                return (outcomes, sha);
            }

            return Unavailable<(IReadOnlyList<RunOutcome>, string)>($"{path} has no scriptSha256 and steps");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return Unavailable<(IReadOnlyList<RunOutcome>, string)>($"ui-evidence wrote no readable {path}: {ex.Message}");
        }
    }

    private static Result<UiRun> Unavailable(string message) => Unavailable<UiRun>(message);

    private static Result<T> Unavailable<T>(string message) => BugFixErrors.UiEvidenceUnavailable.With($"{message}; no run registered; retry run-ui with backoff (up to 3 tries), then advance report --unresolved");
}
