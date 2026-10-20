using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Plan;

/// <summary>The plan packet assembler (line spec §3.2 row 3): the seven files of the candidate packet, graph.json derived by code.</summary>
public static class PlanPacket
{
    private const int SchemaVersion = 1;

    /// <summary>The packet's files.</summary>
    /// <param name="facts">The plan files (tasks.json, exec-review.json, holdout.json, answers.jsonl) with the design packet's under 'design/'.</param>
    /// <param name="identity">The packet's id, repository and base commit.</param>
    /// <param name="parents">The frozen scope packet's hash and the design packet's candidate hash.</param>
    /// <param name="options">The profile values graph.json is derived with.</param>
    /// <returns>The files, in the order the packet kind lists them.</returns>
    public static IReadOnlyList<LineFile> Build(LineFacts facts, PacketIdentity identity, PlanParents parents, PlanGraphOptions options)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(parents);
        ArgumentNullException.ThrowIfNull(options);
        var files = new List<LineFile> { new("packet.json", LineJson.Write(Packet(identity))) };
        AddIfPresent(files, facts, "tasks.json");
        files.Add(new LineFile("index.json", LineJson.Write(Index(facts, parents))));
        if (PlanGraph.Derive(facts.Text("tasks.json"), options) is { } graph)
        {
            files.Add(new LineFile("graph.json", graph));
        }

        foreach (var name in new[] { "exec-review.json", "holdout.json", "answers.jsonl" })
        {
            AddIfPresent(files, facts, name);
        }

        return files;
    }

    private static void AddIfPresent(List<LineFile> files, LineFacts facts, string name)
    {
        if (facts.Has(name))
        {
            files.Add(new LineFile(name, LineJson.Terminated(facts.Text(name))));
        }
    }

    private static JsonObject Packet(PacketIdentity identity) => new()
    {
        ["schemaVersion"] = SchemaVersion,
        ["kind"] = "plan",
        ["id"] = identity.Packet,
        ["repo"] = identity.Repo,
        ["baseSha"] = identity.BaseSha,
        ["status"] = "draft",
        ["hash"] = null,
        ["links"] = new JsonObject
        {
            ["tasks"] = "tasks.json",
            ["graph"] = "graph.json",
            ["execReview"] = "exec-review.json",
            ["holdout"] = "holdout.json",
            ["answers"] = "answers.jsonl",
            ["index"] = "index.json",
        },
    };

    // A plan has no acceptance file of its own: it extends the design's.
    private static JsonObject Index(LineFacts facts, PlanParents parents) => new()
    {
        ["schemaVersion"] = SchemaVersion,
        ["kind"] = "plan",
        ["parents"] = new JsonArray(parents.Design, parents.Scope),
        ["acceptance"] = new JsonObject
        {
            ["self"] = null,
            ["extends"] = facts.Has("design/acceptance.json") ? LineJson.Hash(facts.Text("design/acceptance.json")) : null,
        },
    };
}
