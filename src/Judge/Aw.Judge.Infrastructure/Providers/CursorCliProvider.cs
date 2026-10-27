using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// A small fast model through `cursor-agent -p` on the user's Cursor login (~8–11 s, the slowest, so last by default).
/// Isolation: --workspace the fresh directory (the evidence directory for images), --mode ask (read-only), --sandbox
/// enabled, MCP servers never auto-approved; --trust is required or a fresh workspace exits "Workspace Trust Required".
/// The CLI has no schema mode, so the schema is stated in the prompt and the answer is the last JSON object in the
/// result text (a prose preamble has been seen). Images are named by absolute path, with "do not search" (given a bare
/// name the agent globs, finds nothing and reports the file missing), each on one line. The prompt is an argument, and
/// argv is readable by any local user (ps, /proc/&lt;pid&gt;/cmdline) and recorded by process auditing, so it is redacted
/// first (like Jev's), then capped at <see cref="PromptCap"/> characters (for an image question the screenshot paths
/// included): four bytes each at most, that keeps it under Linux's 128 KiB per argument.
/// </summary>
/// <param name="runner">Runs `cursor-agent`.</param>
/// <param name="redactor">Strips secrets from the prompt before it goes on the command line.</param>
internal sealed class CursorCliProvider(IProcessRunner runner, IInputRedactor redactor) : AgentCliProvider(runner, AgentCliName.CursorCli)
{
    /// <summary>The model.</summary>
    public const string Model = "gpt-5.6-luna-none-fast";

    /// <summary>How many characters of the question's prompt go on the command line.</summary>
    public const int PromptCap = 30_000;

    /// <summary>Of <see cref="PromptCap"/>, how many characters an image question's screenshot paths may take.</summary>
    public const int PathsCap = 2 * UntrustedText.FieldCap;

    /// <inheritdoc />
    protected override AgentCliCall ForText(QuestionRef question, JsonNode? input, string workspace) =>
        Call(question, workspace, $"{UntrustedText.Capped(Redacted(question.Prompt), PromptCap)}\n\n{Instruction(question)}");

    /// <inheritdoc />
    protected override AgentCliCall ForImage(QuestionRef question, ImageEvidence evidence, string workspace) =>
        Call(
            question,
            evidence.EvidenceDir,
            $"{UntrustedText.Capped(Redacted(question.Prompt), PromptCap - PathsCap)}\n\nOpen each screenshot directly with your file-read tool (it reads images), at these paths — do not search for them: {UntrustedText.Capped(string.Join(", ", evidence.Files.Select(UntrustedText.OneLine)), PathsCap)}\n\n{Instruction(question)}");

    /// <inheritdoc />
    protected override ProviderResult Read(string output, QuestionRef question)
    {
        if (WireJson.Read<CliEnvelope>(output) is not { } envelope)
        {
            return new ProviderErrored(ProviderReasons.UnparseableOutput);
        }

        if (envelope.IsError == true)
        {
            return new ProviderErrored(ProviderReasons.CliReportedError);
        }

        return envelope.Result is { } text
            ? AgentAnswer.Read(AgentAnswer.LastObject(text), question)
            : new ProviderErrored(ProviderReasons.UnparseableAnswer);
    }

    // Redact before capping, as everywhere else, so a secret cut at the cap can't survive.
    private string Redacted(string prompt) => redactor.Redact(JsonValue.Create(prompt))?.GetValue<string>() ?? "";

    private static string Instruction(QuestionRef question) =>
        $"Respond with only a single JSON object matching this JSON Schema, and no other text:\n{AnswerSchema.Lax(question).ToJsonString()}";

    private static AgentCliCall Call(QuestionRef question, string workspace, string prompt) =>
        new(["-p", "--output-format", "json", "--model", question.Model ?? Model, "--mode", "ask", "--sandbox", "enabled", "--trust", "--workspace", workspace, prompt], workspace);
}
