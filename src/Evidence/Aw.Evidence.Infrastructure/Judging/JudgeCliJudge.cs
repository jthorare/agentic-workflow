using System.Text;
using System.Text.Json;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Judging;
using Aw.Evidence.Core.Usage;

namespace Aw.Evidence.Infrastructure.Judging;

/// <summary>
/// The judge through the C# judge CLI: "judge &lt;question&gt;" with the input record on stdin and the envelope parsed from
/// stdout (docs/csharp/judge.md, 'Questions'). Exit 0 is a decision; anything else (an escalation, invalid input, a program that
/// would not start, an answer out of contract) is null, so callers fail closed: no repair, a visual left unchecked. The binary is
/// AW_JUDGE_BIN (an absolute path, or a name looked up on PATH), else "judge" on PATH. The runner clears the environment, so
/// only the judge's own settings, provider credentials and DOTNET_ROOT (a framework-dependent build) are passed on.
/// </summary>
internal sealed class JudgeCliJudge(ICliRunner runner, IExecutableLocator locator, Func<string, string?>? environment = null) : IJudge
{
    private const string BinVariable = "AW_JUDGE_BIN";
    private const string DefaultBinary = "judge";

    // The judge's state and provider selection, and the credentials its agent CLIs read; nothing else leaks into the call.
    private static readonly string[] _passedOn =
    [
        "AW_STATE_DIR", "AW_JUDGE_STATE_DIR", "AW_JUDGE_SESSIONS_DIR", "AW_JUDGE_PROVIDERS", "AW_LOG_LEVEL",
        "ANTHROPIC_API_KEY", "OPENAI_API_KEY", "CODEX_HOME", "CLAUDE_CONFIG_DIR", "DOTNET_ROOT",
    ];

    private readonly Func<string, string?> _environment = environment ?? Environment.GetEnvironmentVariable;

    /// <inheritdoc/>
    public bool IsAvailable => FindJudge() is not null;

    /// <inheritdoc/>
    public async ValueTask<SelectorRepair?> RepairSelectorAsync(SelectorRepairRequest request, CancellationToken cancellationToken)
    {
        var answer = await AskAsync("ui-element-repair", RepairInput(request), cancellationToken).ConfigureAwait(false);
        if (answer is null)
        {
            return null;
        }

        using (answer)
        {
            var root = answer.RootElement;
            var usage = UsageOf(root);
            return Text(root, "decision") switch
            {
                "no-good-candidate" => new SelectorRepair(RepairDecision.NoGoodCandidate, ChosenIndex: null, usage),
                "repaired" when ChosenIndex(root, request) is { } chosen => new SelectorRepair(RepairDecision.Repaired, chosen, usage),
                _ => null,
            };
        }
    }

    /// <inheritdoc/>
    public async ValueTask<VisualVerdict?> CritiqueAsync(VisualCritiqueRequest request, CancellationToken cancellationToken)
    {
        var input = CritiqueInput(request);
        if (input is null)
        {
            return null;
        }

        var answer = await AskAsync("visual-critique", input, cancellationToken).ConfigureAwait(false);
        if (answer is null)
        {
            return null;
        }

        using (answer)
        {
            var root = answer.RootElement;
            VisualDecision? decision = Text(root, "decision") switch
            {
                "looks-right" => VisualDecision.LooksRight,
                "looks-off" => VisualDecision.LooksOff,
                "sloppy" => VisualDecision.Sloppy,
                _ => null,
            };
            return decision is { } d ? new VisualVerdict(d, ReasonsOf(root), UsageOf(root)) : null;
        }
    }

    private async ValueTask<JsonDocument?> AskAsync(string question, string input, CancellationToken cancellationToken)
    {
        if (FindJudge() is not { } judge)
        {
            return null;
        }

        var invocation = new CliInvocation(judge, [question], input, PassedOnEnvironment());
        var outcome = await runner.RunAsync(invocation, cancellationToken).ConfigureAwait(false);
        if (outcome.IsError || outcome.Value.ExitCode != 0)
        {
            return null;
        }

        try
        {
            var document = JsonDocument.Parse(outcome.Value.Stdout);
            if (document.RootElement.ValueKind == JsonValueKind.Object && !document.RootElement.TryGetProperty("escalated", out _))
            {
                return document;
            }

            document.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string? FindJudge()
    {
        var configured = Set(_environment(BinVariable)) ?? DefaultBinary;
        if (configured.StartsWith('/'))
        {
            return locator.RealExecutable(configured);
        }

        // A bare name or a relative path is looked up on PATH like a shell would; the runner never searches PATH itself.
        return (_environment("PATH") ?? string.Empty)
            .Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Where(static directory => directory.StartsWith('/'))
            .Select(directory => locator.RealExecutable($"{directory.TrimEnd('/')}/{configured}"))
            .FirstOrDefault(static found => found is not null);
    }

    private Dictionary<string, string> PassedOnEnvironment()
    {
        var passed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in _passedOn)
        {
            if (Set(_environment(name)) is { } value)
            {
                passed[name] = value;
            }
        }

        return passed;
    }

    private static string RepairInput(SelectorRepairRequest request)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("brokenSelector", request.BrokenSelector);
            writer.WriteString("step", request.Step);
            writer.WriteStartArray("candidates");
            foreach (var candidate in request.Candidates)
            {
                writer.WriteStartObject();
                writer.WriteNumber("index", candidate.Index);
                writer.WriteString("role", candidate.Role);
                writer.WriteString("accessibleName", candidate.AccessibleName);
                writer.WriteString("testId", candidate.TestId?.Value);
                writer.WriteString("text", candidate.Text);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // The judge takes each screenshot as a path inside the evidence directory; one outside it is never sent.
    private static string? CritiqueInput(VisualCritiqueRequest request)
    {
        var after = Inside(request.EvidenceDirectory, request.AfterScreenshot);
        var baseline = request.BaselineScreenshot is null ? null : Inside(request.EvidenceDirectory, request.BaselineScreenshot);
        if (after is null || (request.BaselineScreenshot is not null && baseline is null))
        {
            return null;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("afterScreenshot", after);
            writer.WriteString("evidenceDir", request.EvidenceDirectory);
            if (baseline is not null)
            {
                writer.WriteString("baselineScreenshot", baseline);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string? Inside(string directory, string file)
    {
        if (!directory.StartsWith('/') || !file.StartsWith('/'))
        {
            return null;
        }

        var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
        return relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? null : relative;
    }

    // The envelope carries the model but no token counts: those stay unknown, never zero.
    private static InvocationUsage UsageOf(JsonElement root) => new(Text(root, "model"), InputTokens: null, OutputTokens: null);

    private static List<string> ReasonsOf(JsonElement root) =>
        root.TryGetProperty("reasons", out var reasons) && reasons.ValueKind == JsonValueKind.Array
            ? [.. reasons.EnumerateArray().Where(static r => r.ValueKind == JsonValueKind.String).Select(static r => r.GetString()).OfType<string>()]
            : [];

    // A chosen index counts only when it names one of the candidates sent.
    private static int? ChosenIndex(JsonElement root, SelectorRepairRequest request) =>
        root.TryGetProperty("chosenIndex", out var index) && index.ValueKind == JsonValueKind.Number
            && index.TryGetInt32(out var chosen) && request.Candidates.Any(c => c.Index == chosen)
            ? chosen
            : null;

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? Set(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
