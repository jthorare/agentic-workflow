using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.line.report' (line spec BD7, LN-WF1, Q13): the phase tells its line that it has 'shipped', 'merged' or 'failed'; the engine tells it that a
/// linked phase 'parked', 'resumed' or 'ended' (with 'parkReason', or the terminal 'ended' outcome and the 'packet' hash). A phase that belongs to a line
/// (its 'line' fact names one) appends a row to 'line-report.jsonl' in the instance's run area, one per effect identity, and replies nothing; a phase
/// outside a line records nothing. The delivery of 'line.phaseChanged' to the linked parent belongs to the line's dispatcher (LN-WF1), which reads
/// this record; it is not this effect's.
/// </summary>
/// <param name="area">The instance's run area, where the report is recorded.</param>
/// <param name="contents">Reads the line context fact.</param>
/// <param name="store">Stores the 'fact:phaseEvent' the reply carries.</param>
/// <param name="clock">When a report is written.</param>
internal sealed class LineReportEffect(IRunArea area, IFactContents contents, IFactStore store, TimeProvider clock) : IEffectKind, IDescribedBlock
{
    private const string Record = "line-report.jsonl";
    private static readonly string[] _extras = ["parkReason", "ended", "packet"];
    private static readonly string[] _statuses = ["shipped", "merged", "failed", "parked", "resumed", "ended"];
    private static readonly EventKind _phaseChanged = EventKind.From("line.phaseChanged");
    private static readonly FactKey _phaseEventFact = FactKey.From("fact:phaseEvent");

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => LineEffects.Report;

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (Status(context) is not { } status)
        {
            return Failed(context, "status 'shipped', 'merged', 'failed', 'parked', 'resumed' or 'ended'");
        }

        var line = await ReadLine(context, cancellationToken).ConfigureAwait(false);
        if (line.IsError)
        {
            return line.Error;
        }

        // The plan's phase id ("P1") is the packet's; the address carries a lowercase slug of it, so the packet wins over the line context when the engine passes it.
        var phase = await PacketPhase(context, cancellationToken).ConfigureAwait(false) ?? Name(line.Value, "phase");
        return Name(line.Value, "line") is { } name
            ? await Append(context, status, name, phase, cancellationToken).ConfigureAwait(false)
            : new EffectDone(Reply: null, Existed: false);
    }

    /// <summary>The 'phase' field of the stored 'phasePacket' argument (the engine's report passes the linked instance's packet); null when absent or unreadable.</summary>
    private async ValueTask<string?> PacketPhase(EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.Arguments.TryGetValue("phasePacket", out var stored) || stored.ValueKind != JsonValueKind.Object
            || !stored.TryGetProperty("ref", out var reference) || reference.ValueKind != JsonValueKind.String
            || !stored.TryGetProperty("sha256", out var sha) || sha.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var read = await contents.Read(new FactValue(reference.GetString()!, sha.GetString()!, HeadSha: null), cancellationToken).ConfigureAwait(false);   // why: both checked to be strings above
        if (read.IsError)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(read.Value);
            return Name(document.RootElement, "phase");
        }
        catch (JsonException)   // boundary: a packet that isn't JSON names no phase; the line context's is used
        {
            return null;
        }
    }

    private static string? Status(EffectContext context)
    {
        if (!context.Arguments.TryGetValue("status", out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var status = value.GetString();
        return status is not null && _statuses.Contains(status, StringComparer.Ordinal) ? status : null;
    }

    private static string? Name(JsonElement line, string property) =>
        line.ValueKind == JsonValueKind.Object && line.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

    private static Error Failed(EffectContext context, string what) =>
        WorkflowErrors.EffectFailed.With($"{context.Effect.Value} of {context.Instance} needs {what} (fix: pass it in the transition's with:)");

    private async ValueTask<Result<EffectDone>> Append(EffectContext context, string status, string line, string? phase, CancellationToken cancellationToken)
    {
        var before = await area.Read(context.Instance, Record, cancellationToken).ConfigureAwait(false);
        if (before.IsError)
        {
            return before.Error;
        }

        var identity = JsonValue.Create(context.Identity).ToJsonString();
        var recorded = before.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(row => row.Contains("\"identity\":" + identity, StringComparison.Ordinal));
        if (recorded is not null)
        {
            // A repeat replies again with the first attempt's 'at', so the reply fact is the same bytes (the reply's ledger key makes the repeat harmless).
            return await Reply(status, new PhaseEvent(line, phase, context.Instance, At(recorded), Extras(context)), existed: true, cancellationToken).ConfigureAwait(false);
        }

        var at = clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
        var row = new JsonObject
        {
            ["status"] = status,
            ["identity"] = context.Identity,
            ["instance"] = context.Instance,
            ["line"] = line,
            ["phase"] = phase,
            ["at"] = at,
        };
        var written = await area.Write(context.Instance, Record, row.ToJsonString() + "\n", append: true, cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? written.Error
            : await Reply(status, new PhaseEvent(line, phase, context.Instance, at, Extras(context)), existed: false, cancellationToken).ConfigureAwait(false);
    }

    private static string At(string recorded)
    {
        try
        {
            return JsonNode.Parse(recorded)?["at"]?.GetValue<string>() ?? string.Empty;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)   // boundary: a damaged audit row has no time to repeat
        {
            return string.Empty;
        }
    }

    /// <summary>The reply: the status as the outcome of 'line.phaseChanged', carrying 'fact:phaseEvent' { line, phase, attempt?, status, instance, at, parkReason?, ended?, packet? }.</summary>
    private async ValueTask<Result<EffectDone>> Reply(string status, PhaseEvent phaseEvent, bool existed, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["line"] = phaseEvent.Line,
            ["phase"] = phaseEvent.Phase,
            ["status"] = status,
            ["instance"] = phaseEvent.Instance,
            ["at"] = phaseEvent.At,
        };
        if (InstanceAddress.TryFrom(phaseEvent.Instance, out var address) && address.LinkedParent() is not null && AttemptOf(phaseEvent.Instance) is { } attempt)
        {
            payload["attempt"] = attempt;
        }

        foreach (var (name, text) in phaseEvent.Extras)
        {
            payload[name] = text;
        }

        var stored = await store.Put(Encoding.UTF8.GetBytes(payload.ToJsonString()), cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var facts = new Dictionary<FactKey, ReplyFact> { [_phaseEventFact] = new ReplyFact(stored.Value.Ref, stored.Value.Sha256, HeadSha: null) };
        return new EffectDone(OutcomeName.From(status), existed, facts, _phaseChanged);   // why: 'merged' is also an outcome of pr.mergeResult and pr.docsMerged
    }

    private sealed record PhaseEvent(string Line, string? Phase, string Instance, string At, IReadOnlyDictionary<string, string> Extras);

    /// <summary>The optional text arguments the engine's report carries into the event: why the phase parked, how it ended, the packet in force.</summary>
    private static Dictionary<string, string> Extras(EffectContext context) => _extras
        .Select(name => (Name: name, Text: context.Arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null))
        .Where(static e => e.Text is { Length: > 0 })
        .ToDictionary(static e => e.Name, static e => e.Text ?? string.Empty, StringComparer.Ordinal);   // Where kept only the non-empty ones

    /// <summary>The attempt a linked instance's address names, '{line}/phase.{id}.{attempt}'; null for anything else.</summary>
    private static int? AttemptOf(string instance) =>
        instance.LastIndexOf('.') is >= 0 and var dot && int.TryParse(instance.AsSpan(dot + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var attempt) && attempt >= 1 ? attempt : null;

    private async ValueTask<Result<JsonElement>> ReadLine(EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.Arguments.TryGetValue("line", out var stored) || stored.ValueKind != JsonValueKind.Object
            || !stored.TryGetProperty("ref", out var reference) || reference.ValueKind != JsonValueKind.String
            || !stored.TryGetProperty("sha256", out var sha) || sha.ValueKind != JsonValueKind.String)
        {
            return Failed(context, "'line' as a stored fact (the line context)");
        }

        var read = await contents.Read(new FactValue(reference.GetString()!, sha.GetString()!, HeadSha: null), cancellationToken).ConfigureAwait(false);   // why: both checked to be strings above
        if (read.IsError)
        {
            return read.Error;
        }

        try
        {
            using var document = JsonDocument.Parse(read.Value);
            return document.RootElement.Clone();
        }
        catch (JsonException)   // boundary: a fact that isn't JSON names no line
        {
            return Failed(context, "'line' as JSON");
        }
    }
}
