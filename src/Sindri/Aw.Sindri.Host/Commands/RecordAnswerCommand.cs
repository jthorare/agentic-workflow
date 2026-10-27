using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri workflow answer &lt;address&gt; --record &lt;batch.json&gt; [--nonce &lt;n&gt;] [--json]' (#1270): hands the interview desk an answer batch without the desk
/// form. It reads the instance, finds the open run of 'step.interview.answer' through <see cref="AnswerRecording"/> (any other state is refused with
/// "rewind to interview first"), and reports that run 'answered' with the file as 'fact:answers' through the same 'complete' route the desk uses, so
/// 'step.interview.record' runs as normal. A batch may carry 'supersedes' (earlier answer ids) and must carry 'recordedAs' naming ids the facts hold,
/// or an item, surface or open question must cite its id, or SCP-ACC-003 fails on it like on any answer.
/// </summary>
internal static class RecordAnswerCommand
{
    private const string Usage = "usage: sindri workflow answer <address> --record <batch.json> [--nonce <n>] [--json] (the batch: a JSON array of answer objects with 'answers', optional 'supersedes', and 'recordedAs' naming ids the facts hold)";

    /// <summary>What the command needs from the verb table.</summary>
    /// <param name="Desk">The API client.</param>
    /// <param name="Services">The scope the clock resolves from.</param>
    /// <param name="Output">Where the verb writes.</param>
    internal sealed record Host(RecipeDesk Desk, IServiceProvider Services, Output Output);

    /// <summary>Records the batch; exit 0 applied, 1 not running, 2 invalid, refused or error.</summary>
    internal static async Task<int> Run(string address, string[] options, bool json, Host host, CancellationToken ct)
    {
        var parsed = Parse(options);
        if (parsed.IsError)
        {
            return await host.Output.Error(parsed.Error).ConfigureAwait(false);
        }

        var (file, nonce) = parsed.Value;
        var id = await InstanceReads.IdOf(host.Desk, address, ct).ConfigureAwait(false);
        var read = id.IsError ? id.Error : await host.Desk.Client.Get<InstanceDetail>($"{InstanceReads.Instances}/{InstanceRoute.Segment(id.Value.Value)}", ct).ConfigureAwait(false);
        if (read.IsError)
        {
            return await host.Output.Error(read.Error).ConfigureAwait(false);
        }

        var run = AnswerRecording.InterviewRun(read.Value.Value);
        if (run.IsError)
        {
            return await host.Output.Error(run.Error).ConfigureAwait(false);
        }

        if (await UnknownSupersedes(host.Desk, id.Value.Value, file, ct).ConfigureAwait(false) is { } refusal)
        {
            return await host.Output.Error(refusal).ConfigureAwait(false);
        }

        string[] complete = [address, "--run", run.Value.Value, "--outcome", "answered", "--output", $"fact:answers={file}", .. nonce is null ? [] : new[] { "--nonce", nonce }, .. json ? new[] { "--json" } : []];
        return await CompleteCommand.Run(complete, host.Desk, host.Services, host.Output, ct).ConfigureAwait(false);
    }

    /// <summary>The refusal for a batch whose 'supersedes' names an answer the log does not hold: a typo would leave the old answer in place, so it is named before anything is sent.</summary>
    private static async Task<Error?> UnknownSupersedes(RecipeDesk desk, string id, string file, CancellationToken ct)
    {
        JsonNode? batch;
        try
        {
            batch = JsonNode.Parse(await File.ReadAllTextAsync(file, ct).ConfigureAwait(false));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)   // boundary: an unreadable or malformed file is the caller's input error
        {
            return SindriErrors.InvalidInput.With($"{file} can't be read as JSON: {e.Message} ({Usage})");
        }

        var superseded = (batch is JsonArray array ? array : [batch]).OfType<JsonObject>().SelectMany(static b => (b["supersedes"] as JsonArray ?? []).Select(static s => s?.GetValue<string>())).OfType<string>().ToList();
        if (superseded.Count == 0)
        {
            return null;
        }

        var log = await desk.Client.Get<FactView>($"{InstanceReads.Instances}/{id}/facts/{Uri.EscapeDataString("fact:answerLog")}", ct).ConfigureAwait(false);
        if (log.IsError)
        {
            return log.Error;
        }

        var held = log.Value.Value.Body.EnumerateArray().Select(static e => e.TryGetProperty("id", out var v) ? v.GetString() : null).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return superseded.Where(s => !held.Contains(s)).ToList() is { Count: > 0 } missing
            ? SindriErrors.InvalidInput.With($"the batch supersedes {string.Join(", ", missing)}, which the answer log does not hold (it holds {string.Join(", ", held)}; fix: name ids from that list)")
            : null;
    }

    private static Result<(string File, string? Nonce)> Parse(string[] options)
    {
        string? file = null;
        string? nonce = null;
        for (var i = 0; i < options.Length; i += 2)
        {
            if (i + 1 >= options.Length)
            {
                return SindriErrors.InvalidInput.With($"'{options[i]}' needs a value ({Usage})");
            }

            switch (options[i])
            {
                case "--record":
                    file = options[i + 1];
                    break;
                case "--nonce" when CommandNonce.TryFrom(options[i + 1], out _):
                    nonce = options[i + 1];
                    break;
                default:
                    return SindriErrors.InvalidInput.With($"'{options[i]} {options[i + 1]}' isn't an option next to --record ({Usage})");
            }
        }

        return file is null ? SindriErrors.InvalidInput.With(Usage) : (file, nonce);
    }
}
