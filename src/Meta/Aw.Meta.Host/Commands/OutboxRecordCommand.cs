using Aw.Kernel;
using Aw.Meta.Features.OutboxRecord;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'outbox-record' command, the PostToolUse hook's call: standard input is the hook's JSON. Prints the meta session's recorded send and a STATUS: send's warning (any session); otherwise nothing.</summary>
internal static class OutboxRecordCommand
{
    internal static CommandEntry Entry { get; } = new(
        "outbox-record",
        CommandSpec.None("outbox-record [--json] < PostToolUse JSON"),
        NeedsConfig: false,   // sends by any other session are ignored, with or without a team
        Run);

    private static async Task<int> Run(CommandContext context)
    {
        var payload = await context.Io.In.ReadToEndAsync(context.Cancellation).ConfigureAwait(false);
        var request = PostToolUseParser.Parse(payload);
        if (request.IsError)
        {
            return CommandOutput.Fail(context, request.Error);
        }

        return request.Value is { } send
            ? await CommandOutput.Call<OutboxRecordRequest, OutboxRecordResponse>(context, send, Text).ConfigureAwait(false)
            : CommandOutput.Report(context, Results.Success(new OutboxRecordResponse(Recorded: false, [])), Text);
    }

    private static string Text(OutboxRecordResponse response)
    {
        var line = !response.Recorded
            ? string.Empty
            : response.Closed.Count == 0
                ? "recorded the send; no obligation closed"
                : "recorded the send; closed: " + string.Join("; ", response.Closed.Select(o => o.Summary));
        return response.Warning is null ? line : line.Length == 0 ? response.Warning : response.Warning + "\n" + line;
    }
}
