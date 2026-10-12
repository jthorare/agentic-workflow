using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// Haiku through `claude -p` on the user's subscription. Isolation: no session file, no MCP servers, no user, project
/// or local settings and no CLAUDE.md (--setting-sources ""), hooks and slash commands off, thinking off
/// (MAX_THINKING_TOKENS=0 and alwaysThinkingEnabled false: ~2.7 s instead of ~15 s). The question's prompt is the
/// message, on standard input (`claude -p` reads it there when no prompt argument is given). A text question runs with
/// no tools; an image question runs in the evidence directory with only the Read tool, scoped to it. The answer is --json-schema's structured_output, else the result
/// text parsed as JSON (never free prose).
/// </summary>
/// <param name="runner">Runs `claude`.</param>
internal sealed class ClaudeCliProvider(IProcessRunner runner) : AgentCliProvider(runner, AgentCliName.ClaudeCli)
{
    private const string TextInstruction = "You answer one classification question. Answer only with the JSON object the schema describes.";

    private const string Settings = """{"disableAllHooks":true,"alwaysThinkingEnabled":false}""";

    /// <inheritdoc />
    protected override AgentCliCall ForText(QuestionRef question, JsonNode? input, string workspace) =>
        new([.. Common, "--tools", "", "--system-prompt", TextInstruction, "--json-schema", AnswerSchema.Lax(question).ToJsonString(), "--output-format", "json"], workspace)
        {
            Environment = Fast,
            StandardInput = question.Prompt,
        };

    /// <inheritdoc />
    protected override AgentCliCall ForImage(QuestionRef question, ImageEvidence evidence, string workspace) =>
        new(
            [
                .. Common, "--tools", "Read", "--allowedTools", "Read(./**)", "--json-schema", AnswerSchema.Lax(question).ToJsonString(),
                "--system-prompt", "You inspect screenshots. Use the Read tool on the given path, then answer.", "--output-format", "json",
            ],
            evidence.EvidenceDir)
        {
            Environment = Fast,
            StandardInput = question.Prompt,
        };

    /// <inheritdoc />
    protected override ProviderResult Read(string output, QuestionRef question)
    {
        if (WireJson.Read<CliEnvelope>(output) is not { } envelope)
        {
            return new ProviderErrored(ProviderReasons.UnparseableOutput);
        }

        if (envelope.StructuredOutput is { } structured)
        {
            return AgentAnswer.Read(structured, question);
        }

        if (envelope.IsError == true)
        {
            return new ProviderErrored(ProviderReasons.CliReportedError);
        }

        return envelope.Result is { } text && WireJson.TryParse(text, out var answer)
            ? AgentAnswer.Read(answer, question)
            : new ProviderErrored(ProviderReasons.UnparseableAnswer);
    }

    private static IReadOnlyDictionary<string, string> Fast { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["MAX_THINKING_TOKENS"] = "0" };

    private static IReadOnlyList<string> Common { get; } =
    [
        "-p", "--model", "haiku", "--effort", "low", "--no-session-persistence",
        "--strict-mcp-config", "--mcp-config", """{"mcpServers":{}}""",
        "--setting-sources", "", "--settings", Settings, "--disable-slash-commands",
    ];
}
