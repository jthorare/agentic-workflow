using System.Text.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The 'scoping.approved' row (scoping spec §3 'Freeze', SC3; api #503): when 'step.scope.freeze' reports 'frozen', the engine appends it under the instance's lease, since a
/// Step has no ledger port. The row names the instance by its address and is about its item; its hash is the one the Step froze (aw-packets' hash of the run area's
/// <c language="text">scope-packet/</c>, which stays in place), and its ids and counts are read from that frozen directory, so the row says what the packet says. The key is
/// 'scoping.approved:&lt;packetHash&gt;', so a freeze reported again writes nothing and a re-scope that freezes a new hash appends a new row. A row that cannot be appended
/// fails the Step: nobody sees a freeze the ledger doesn't hold.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="lease">The lease this process holds; the append is fenced by it.</param>
/// <param name="events">The control plane's event factory.</param>
/// <param name="area">The instance's run area.</param>
/// <param name="packets">Hashes the kept packet again, so the row's hash is aw-packets' own and not only what the Step said.</param>
internal sealed class ScopingApprovals(ILedgerStore ledger, HeldLease lease, EventFactory events, IRunArea area, IPackets packets)
{
    private const string Packet = "scope-packet/";
    private const string Rescope = "rescope";

    /// <summary>The Step whose 'frozen' report is an approval.</summary>
    public static BlockId Freeze { get; } = BlockId.From("step.scope.freeze");

    /// <summary>Appends the row when <paramref name="result"/> is the freeze Step's 'frozen'.</summary>
    /// <param name="step">The Step that ran.</param>
    /// <param name="context">What it ran with.</param>
    /// <param name="result">What it returned.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The result, or a failed one when the row could not be appended.</returns>
    public async ValueTask<BlockStepResult> After(BlockId step, StepContext context, BlockStepResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        if (step != Freeze || result.Result is not { } done || done.Outcome.Value != "frozen")
        {
            return result;
        }

        if (lease.Current is not { } grant)
        {
            return BlockStepResult.Failed("SND-LEASE-002: this process holds no lease, so scoping.approved can't be recorded");
        }

        if (context.Address is not { } address || !InstanceAddress.TryFrom(address, out var parsed) || Hash(done) is not { } hash)
        {
            return BlockStepResult.Failed($"{Freeze.Value} froze the packet, but the engine has no address or hash to record scoping.approved with");
        }

        try
        {
            var kept = await KeptHash(address, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(kept, hash, StringComparison.Ordinal))
            {
                return BlockStepResult.Failed($"scoping.approved can't be recorded: the kept scope packet hashes to {kept}, not the {hash} the Step froze");
            }

            var payload = await Payload(address, hash, cancellationToken).ConfigureAwait(false);
            var entry = events.Entry(EventTypes.ScopingApproved, payload, $"{EventTypes.ScopingApproved}:{hash}", parsed.Value[..parsed.Value.IndexOf('/', StringComparison.Ordinal)]);   // why: a parsed address always starts with its item and a slash
            var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
            return appended.IsError ? BlockStepResult.Failed($"scoping.approved can't be recorded: {appended.Error.Code}: {appended.Error.Message}") : result;
        }
        catch (JsonException e)
        {
            return BlockStepResult.Failed($"scoping.approved can't be recorded: the frozen packet is not readable: {e.Message}");   // boundary: the packet's files are text the owner approved
        }
        catch (InvalidOperationException e)
        {
            return BlockStepResult.Failed($"scoping.approved can't be recorded: {e.Message}");
        }
    }

    private async Task<string> KeptHash(string address, CancellationToken cancellationToken)
    {
        var dir = area.PathFor(address, Packet.TrimEnd('/'));
        if (dir.IsError)
        {
            throw new InvalidOperationException(dir.Error.Message);
        }

        var hash = await packets.Hash(new PacketRef(PacketKind.Scoping, dir.Value), cancellationToken).ConfigureAwait(false);
        return hash.IsError ? throw new InvalidOperationException($"aw-packets can't hash the kept packet: {hash.Error.Message}") : hash.Value;
    }

    private static string? Hash(StepResult done) =>
        done.Outputs.TryGetValue(FactKey.From("fact:scopePacket"), out var packet) && Parsed(packet) is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty("hash", out var hash) && hash.ValueKind == JsonValueKind.String
            ? hash.GetString()
            : null;

    // the runner stores a fact as its text, so an object output may arrive as a JSON string holding the object
    private static JsonElement Parsed(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return element;
        }

        using var document = JsonDocument.Parse(element.GetString() ?? "null");
        return document.RootElement.Clone();
    }

    private async Task<ScopingApproved> Payload(string address, string hash, CancellationToken cancellationToken)
    {
        var map = await Read(address, "scope-map.json", cancellationToken).ConfigureAwait(false);
        var acceptance = await Read(address, "acceptance.json", cancellationToken).ConfigureAwait(false);
        var rounds = await Read(address, "rounds.jsonl", cancellationToken).ConfigureAwait(false);
        var lines = rounds.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new ScopingApproved(hash, address, Ids(map, "surfaces"), Ids(acceptance, "items"), lines.Length, lines.Count(IsRescope));
    }

    private static bool IsRescope(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("trigger", out var trigger) && trigger.ValueKind == JsonValueKind.String && trigger.GetString() == Rescope;
    }

    private async Task<string> Read(string address, string name, CancellationToken cancellationToken)
    {
        var read = await area.Read(address, Packet + name, cancellationToken).ConfigureAwait(false);
        return read.IsError ? throw new JsonException($"{Packet}{name}: {read.Error.Message}") : read.Value;
    }

    private static List<string> Ids(string json, string list)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(list, out var items) && items.ValueKind == JsonValueKind.Array
            ? [.. items.EnumerateArray().Where(static i => i.ValueKind == JsonValueKind.Object && i.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String).Select(static i => i.GetProperty("id").GetString()!)]   // why: the filter checked the id is a string
            : [];
    }
}
