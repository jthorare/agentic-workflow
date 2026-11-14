using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

public sealed partial class PromptBlock
{
    private async ValueTask<Result<JsonObject>> AskForObject(BlockContext context, string prompt, JsonObject schema, CancellationToken cancellationToken)
    {
        var instructed = WithInstruction(prompt, schema);
        var asked = await AskChecked(context, instructed, cancellationToken).ConfigureAwait(false);
        if (asked.IsError)
        {
            return asked.Error;
        }

        // why: set before the parse, so a JSON answer the schema check then refuses also shows on the failed step; the engine reads it only when the block returns an error
        context.LeaveFailureOutput(new JsonObject { ["answer"] = asked.Value });
        var read = Parse(asked.Value);
        if (read.IsSuccess && !IsSchemaEcho(read.Value, schema))
        {
            return read;
        }

        // why: one repair ask is cheap and most prose answers (and schema copies, #3588) turn into a real answer when told; a second miss is the model's, so it fails in words
        var again = await AskChecked(context, instructed + (read.IsSuccess ? EchoRepairLine : RepairLine), cancellationToken).ConfigureAwait(false);
        if (again.IsError)
        {
            return again.Error;
        }

        // why: the run page shows the failed step's output, so the raw answer stays there for the owner to read; the engine cuts it to the stored-output cap
        context.LeaveFailureOutput(new JsonObject { ["answer"] = again.Value });
        read = Parse(again.Value);
        if (read.IsSuccess && !IsSchemaEcho(read.Value, schema))
        {
            return read;
        }

        context.LeaveRaw(again.Value);

        return read.IsSuccess
            ? SindriErrors.GraphPromptAnswer.With($"The model answered with the schema itself, not an answer that fills it: \"{Shown(again.Value)}\"")
            : SindriErrors.GraphPromptAnswer.With($"The model answered with text, not the JSON this step asks for: \"{Shown(again.Value)}\"");
    }

    // why: the CLI's exit code is checked before the text is read: a non-zero exit (a usage limit, a crash) is the model call failing, so there is nothing to repair and no second ask
    private async ValueTask<Result<string>> AskChecked(BlockContext context, string prompt, CancellationToken cancellationToken)
    {
        var answered = await model.Answer(prompt, cancellationToken).ConfigureAwait(false);
        if (answered.IsError)
        {
            return answered.Error;
        }

        var (text, exitCode) = (answered.Value.Text, answered.Value.ExitCode);
        if (exitCode == 0)
        {
            return text;
        }

        context.LeaveFailureOutput(new JsonObject { ["answer"] = text });
        context.LeaveRaw(text);
        return SindriErrors.GraphPromptAnswer.With($"The model answered with text, not the JSON this step asks for: \"{Shown(text)}\"");
    }

    // why: only a reference that exists in the prompt and resolves to nothing is named; the first one in the text is the one the owner fixes first
    private static string? FirstEmptyReference(string raw, Func<string, JsonNode?> resolve)
    {
        var from = 0;
        while (raw.IndexOf("{{", from, StringComparison.Ordinal) is var open and >= 0 && raw.IndexOf("}}", open + 2, StringComparison.Ordinal) is var close and >= 0)
        {
            var reference = raw[(open + 2)..close].Trim();
            if (reference.Length > 0 && resolve(reference) is null)
            {
                return reference;
            }

            from = close + 2;
        }

        return null;
    }

    // why: the message lands in the one-line runs-list row, so line breaks go and a long answer shows its first 200 characters with "…"
    private static string Shown(string answer)
    {
        var flat = WhitespaceRun.Replace(answer, " ").Trim();
        if (flat.Length <= ShownLength)
        {
            return flat;
        }

        var cut = char.IsHighSurrogate(flat[ShownLength - 1]) ? ShownLength - 1 : ShownLength;
        return string.Concat(flat.AsSpan(0, cut), "…");
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WhitespaceRun { get; }
}
