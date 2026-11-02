using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// A small fast OpenAI model through `codex exec` on the user's ChatGPT login. Isolation: -C the fresh workspace,
/// --ignore-user-config, --ignore-rules, --ephemeral, a read-only sandbox, and the hooks feature (with the tool-list
/// features) disabled; every --disable name must exist in the installed CLI or it exits "Unknown feature flag". The
/// answer schema is written to schema.json in the workspace in OpenAI strict mode (--output-schema rejects any other).
/// Images go as --image files; the prompt goes on standard input ('-' after '--', because --image is variadic). The answer is the last
/// agent_message of the --json event lines.
/// </summary>
/// <param name="runner">Runs `codex`.</param>
internal sealed class CodexCliProvider(IProcessRunner runner) : AgentCliProvider(runner, AgentCliName.CodexCli)
{
    /// <summary>The model.</summary>
    public const string Model = "gpt-6-luna";

    private const string UsageLimitExceeded = "usage_limit_exceeded";

    // The pattern id when the event says so itself (codex_error_info), as opposed to a message the matcher read.
    private const string UsageLimitExceededPattern = "codex-error-info";

    private static readonly string[] _disabledFeatures = ["hooks", "apps", "plugins", "multi_agent", "shell_tool", "unified_exec"];

    /// <inheritdoc />
    protected override AgentCliCall ForText(QuestionRef question, JsonNode? input, string workspace) =>
        Call(question, workspace, [], question.Prompt);

    /// <inheritdoc />
    protected override AgentCliCall ForImage(QuestionRef question, ImageEvidence evidence, string workspace) =>
        Call(
            question,
            workspace,
            [.. evidence.Files.SelectMany(file => new[] { "--image", file })],
            $"{question.Prompt}\n\nThe screenshots are attached to this message as images, in the order they are named above.");

    /// <inheritdoc />
    protected override ProviderResult Read(string output, QuestionRef question)
    {
        var run = Scan(output);
        if (run.Message is null)
        {
            return run.Failed ? Errored(ProviderReasons.CliReportedError, run.LimitPattern) : new ProviderErrored(ProviderReasons.UnparseableOutput);
        }

        return WireJson.TryParse(run.Message, out var answer)
            ? ProviderUsageReader.Attach(AgentAnswer.Read(answer, question), run.Usage)
            : new ProviderErrored(ProviderReasons.UnparseableAnswer);
    }

    /// <inheritdoc />
    protected override string? LimitIn(string output) => Scan(output).LimitPattern;

    /// <summary>The usage-limit pattern of an error event: its codex_error_info when it has one, else its message, anchored.</summary>
    private static string? LimitOf(CodexEvent codexEvent)
    {
        if (codexEvent.CodexErrorInfo is { } info)
        {
            return info is JsonValue value && value.TryGetValue<string>(out var kind) && Is(kind, UsageLimitExceeded) ? UsageLimitExceededPattern : null;
        }

        return UsageLimitMatcher.Match(ErrorMessage(codexEvent));
    }

    private static string? ErrorMessage(CodexEvent codexEvent) => codexEvent.Message ?? codexEvent.Error switch
    {
        JsonObject error when error["message"] is JsonValue text && text.TryGetValue<string>(out var message) => message,
        JsonValue text when text.TryGetValue<string>(out var message) => message,
        _ => null,
    };

    // One pass over the --json lines: the last agent message, whether a turn failed or an error came, the limit pattern of the
    // first error event that is one (only error and turn.failed events are searched, never an agent message), and the usage.
    private static CodexRun Scan(string output)
    {
        var events = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(WireJson.Read<CodexEvent>)
            .OfType<CodexEvent>()
            .ToList();
        var failures = events.Where(codexEvent => Is(codexEvent.Type, "turn.failed") || Is(codexEvent.Type, "error")).ToList();
        return new CodexRun(
            events.Select(AgentMessage).LastOrDefault(text => text is not null),
            failures.Count > 0,
            failures.Select(LimitOf).FirstOrDefault(pattern => pattern is not null),
            ProviderUsageReader.FromCodex(events.LastOrDefault(codexEvent => Is(codexEvent.Type, "turn.completed") && codexEvent.Usage is not null)?.Usage));
    }

    private sealed record CodexRun(string? Message, bool Failed, string? LimitPattern, ProviderUsage? Usage);

    private static bool Is(string? value, string expected) => string.Equals(value, expected, StringComparison.Ordinal);

    private static string? AgentMessage(CodexEvent? codexEvent) =>
        Is(codexEvent?.Type, "item.completed") && Is(codexEvent?.Item?.Type, "agent_message") ? codexEvent?.Item?.Text : null;

    private static AgentCliCall Call(QuestionRef question, string workspace, IReadOnlyList<string> images, string prompt)
    {
        var schema = Path.Combine(workspace, "schema.json");
        File.WriteAllText(schema, AnswerSchema.Strict(question).ToJsonString());
        return new AgentCliCall(
            [
                "exec", "--model", question.Model ?? Model, "-c", "model_reasoning_effort=\"low\"", "--ephemeral", "--skip-git-repo-check",
                "--ignore-user-config", "--ignore-rules", .. _disabledFeatures.SelectMany(feature => new[] { "--disable", feature }),
                "--sandbox", "read-only", "-C", workspace, "--output-schema", schema, "--json", .. images, "--", "-",
            ],
            workspace)
        {
            StandardInput = prompt,
        };
    }
}
