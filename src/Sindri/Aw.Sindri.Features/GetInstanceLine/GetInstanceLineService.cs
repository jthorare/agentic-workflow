using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.GetInstanceLine;

/// <summary>
/// The line board of a workflow.line instance (line spec LN-API1): the packet chain from the line's packet facts and the freeze records, and one row for each phase of
/// <c language="text">fact:phaseBoard</c> joined with its linked instance '{line}/phase.{id}.{attempt}' (its next sentence, tokens, wall clock, and the pull request, base and worktree its
/// facts hold). Read-only; text comes out scrubbed, as the fact route scrubs it. The Host fences the text for a session caller.
/// </summary>
/// <param name="instances">Opens the line for the caller.</param>
/// <param name="reader">Reads the phases' linked instances.</param>
/// <param name="contents">The stored fact content.</param>
/// <param name="scrubber">Removes secrets from the text.</param>
/// <param name="frozen">The freeze records.</param>
/// <param name="profile">The approved profile, whose <c language="text">workflow.line</c> keys are the limits shown.</param>
/// <param name="clock">What the wall clock of a running phase is measured to.</param>
internal sealed class GetInstanceLineService(LineInstances instances, IInstanceReader reader, IFactContents contents, ITextScrubber scrubber, IFrozenPackets frozen, IApprovedProfile profile, TimeProvider clock)
    : IService<GetInstanceLineRequest, LineView>
{
    private static readonly RecipeId _phaseRecipe = RecipeId.From("workflow.phase");
    private static readonly (string Station, string Recipe, string Fact)[] _stations =
    [
        ("scoping", "workflow.scoping", "scopePacket"),
        ("design", "workflow.design", "designPacket"),
        ("plan", "workflow.plan", "planPacket"),
    ];

    public async ValueTask<Result<LineView>> Handle(GetInstanceLineRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var opened = await instances.Open(request.Instance, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        var limits = await Limits(cancellationToken).ConfigureAwait(false);
        var stations = limits.IsError ? limits.Error : await Stations(opened.Value, cancellationToken).ConfigureAwait(false);
        var phases = stations.IsError ? stations.Error : await Phases(opened.Value, cancellationToken).ConfigureAwait(false);
        return phases.IsError ? phases.Error : new LineView(stations.Value, phases.Value, limits.Value.Parallel, limits.Value.MaxOpenPrs);
    }

    private async ValueTask<Result<(int Parallel, int MaxOpenPrs)>> Limits(CancellationToken cancellationToken)
    {
        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var settings = approved.Value?.Profile ?? new SindriProfile(1, string.Empty);   // nothing approved: the defaults
        return (LineProfile.Parallel(settings), LineProfile.MaxOpenPrs(settings));
    }

    private async ValueTask<Result<IReadOnlyList<LineStation>>> Stations(InstanceView line, CancellationToken cancellationToken)
    {
        var stations = new List<LineStation>(_stations.Length);
        foreach (var (station, recipe, fact) in _stations)
        {
            var packet = await Packet(line, station, fact, cancellationToken).ConfigureAwait(false);
            if (packet.IsError)
            {
                return packet.Error;
            }

            stations.Add(new LineStation(station, Child(line, recipe), packet.Value));
        }

        return stations;
    }

    private async ValueTask<Result<IReadOnlyList<LinePhaseView>>> Phases(InstanceView line, CancellationToken cancellationToken)
    {
        var boardFact = await Fact(line, "phaseBoard", cancellationToken).ConfigureAwait(false);
        if (boardFact.IsError)
        {
            return boardFact.Error;
        }

        var phases = new List<LinePhaseView>();
        foreach (var row in BoardRow.Read(boardFact.Value))
        {
            var phase = await Phase(line, row, cancellationToken).ConfigureAwait(false);
            if (phase.IsError)
            {
                return phase.Error;
            }

            phases.Add(phase.Value);
        }

        return phases;
    }

    // The station's live child (an ended one is not among the line's children).
    private static string? Child(InstanceView line, string recipe)
    {
        var prefix = "/" + RecipeId.From(recipe).Name + ".";
        return line.Children.LastOrDefault(c => c.Address.Value.Contains(prefix, StringComparison.Ordinal) && c.Address.LinkedParent() is null)?.Address.Value;
    }

    private async ValueTask<Result<LinePacket?>> Packet(InstanceView line, string kind, string fact, CancellationToken cancellationToken)
    {
        var node = await Fact(line, fact, cancellationToken).ConfigureAwait(false);
        if (node.IsError)
        {
            return node.Error;
        }

        var hash = Text(node.Value, "hash");
        if (string.IsNullOrWhiteSpace(hash))
        {
            return Results.Success<LinePacket?>(value: null);
        }

        var found = await frozen.Find(hash, cancellationToken).ConfigureAwait(false);
        if (found.IsError && found.Error.Kind != ErrorKind.Invalid)
        {
            return found.Error;   // the records could not be read: that is not "not frozen"
        }

        return new LinePacket(kind, hash, found.IsSuccess && found.Value is not null);
    }

    private async ValueTask<Result<LinePhaseView>> Phase(InstanceView line, BoardRow row, CancellationToken cancellationToken)
    {
        var parkReason = row.ParkReason is null ? null : scrubber.Scrub(row.ParkReason);
        var bare = new LinePhaseView(row.Phase, row.Attempt, Address: null, row.State, parkReason, Pr: null, Base: null, Worktree: null, Now: string.Empty, Tokens: 0, WallMinutes: 0);
        if (row.Attempt < 1)
        {
            return bare;
        }

        var address = line.Address.Linked(_phaseRecipe, row.Phase.Value, row.Attempt);
        var instance = await reader.Get(address, cancellationToken).ConfigureAwait(false);
        if (instance.IsError)
        {
            return bare with { Address = address.Value };   // why: a started phase whose instance can't be read (not yet visible, or purged) still shows its board row
        }

        var facts = await PhaseFacts(instance.Value, cancellationToken).ConfigureAwait(false);
        return facts.IsError ? facts.Error : Joined(bare with { Address = address.Value }, instance.Value, facts.Value);
    }

    // The phase's row joined with what its instance shows now.
    private LinePhaseView Joined(LinePhaseView row, InstanceView view, (JsonNode? Packet, JsonNode? Workspace, JsonNode? Pr) facts)
    {
        var end = view.Outcome is null ? clock.GetUtcNow() : view.Since;
        return row with
        {
            Pr = Text(facts.Pr, "url", "link", "pr"),
            Base = Base(facts.Packet),
            Worktree = Text(facts.Workspace, "path"),
            Now = scrubber.Scrub(view.Next),
            Tokens = view.Budget?.Instance ?? 0,
            WallMinutes = view.StartedAt is { } started ? (int)Math.Max(0, (end - started).TotalMinutes) : 0,
        };
    }

    private async ValueTask<Result<(JsonNode? Packet, JsonNode? Workspace, JsonNode? Pr)>> PhaseFacts(InstanceView view, CancellationToken cancellationToken)
    {
        var packet = await Fact(view, "phasePacket", cancellationToken).ConfigureAwait(false);
        var workspace = await Fact(view, "workspace", cancellationToken).ConfigureAwait(false);
        var pr = await Fact(view, "pr", cancellationToken).ConfigureAwait(false);
        if (packet.IsError || workspace.IsError || pr.IsError)
        {
            return packet.IsError ? packet.Error : workspace.IsError ? workspace.Error : pr.Error;
        }

        return (packet.Value, workspace.Value, pr.Value);
    }

    // The base the line chose for the phase: the head branch of the stack parent, else the default branch.
    private static string? Base(JsonNode? packet) => LineJson.Obj(packet, "base") is { } chosen
        ? string.Equals(LineJson.Str(chosen, "rule"), "dependency-head", StringComparison.Ordinal) && LineJson.Str(chosen, "ref") is { Length: > 0 } head ? head : "default-branch"
        : null;

    // A fact that is a bare string or an object holding the text under one of the names.
    private static string? Text(JsonNode? fact, params string[] names)
    {
        if (fact is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return names.Select(n => LineJson.Str(fact, n)).FirstOrDefault(static t => !string.IsNullOrEmpty(t));
    }

    private async ValueTask<Result<JsonNode?>> Fact(InstanceView view, string key, CancellationToken cancellationToken)
    {
        if (view.FactShas?.GetValueOrDefault("fact:" + key) is not { } sha)
        {
            return Results.Success<JsonNode?>(value: null);
        }

        var bytes = await contents.Read(new FactValue("fact:sha256:" + sha, sha, HeadSha: null), cancellationToken).ConfigureAwait(false);
        if (bytes.IsError)
        {
            return bytes.Error;
        }

        try
        {
            return Results.Success(JsonNode.Parse(scrubber.Scrub(Encoding.UTF8.GetString(bytes.Value.Span))));
        }
        catch (System.Text.Json.JsonException)
        {
            return Results.Success<JsonNode?>(value: null);   // boundary: a fact that is not JSON shows as absent
        }
    }
}
