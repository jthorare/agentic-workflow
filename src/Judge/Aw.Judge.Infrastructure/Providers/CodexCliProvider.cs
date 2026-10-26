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
        string? message = null;
        var failed = false;
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var codexEvent = WireJson.Read<CodexEvent>(line);
            failed |= Is(codexEvent?.Type, "turn.failed") || Is(codexEvent?.Type, "error");
            message = AgentMessage(codexEvent) ?? message;
        }

        if (message is null)
        {
            return new ProviderErrored(failed ? ProviderReasons.CliReportedError : ProviderReasons.UnparseableOutput);
        }

        return WireJson.TryParse(message, out var answer) ? AgentAnswer.Read(answer, question) : new ProviderErrored(ProviderReasons.UnparseableAnswer);
    }

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
