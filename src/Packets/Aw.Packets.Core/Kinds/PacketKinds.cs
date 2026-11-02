namespace Aw.Packets.Core.Kinds;

/// <summary>The packet-kind registry: every kind aw-packets knows, by name.</summary>
public static class PacketKinds
{
    /// <summary>The build packet (addendum §17.4): 'packet.json' and the files it links.</summary>
    public static PacketKind Build { get; } = new("build", []);

    /// <summary>
    /// The scope packet of the scoping station (scoping spec §8.3): fifteen fixed files. 'lenses.json' has a typed
    /// schema; the others are checked for their shape only, and Sindri's 'ScopePacketChecks' owns what they mean.
    /// </summary>
    public static PacketKind Scoping { get; } = new("scoping",
    [
        new("scope-map.json", PacketFileFormat.Json),
        new("acceptance.json", PacketFileFormat.Json),
        new("coverage.json", PacketFileFormat.Json),
        new("lenses.json", PacketFileFormat.Json),
        new("relevance.json", PacketFileFormat.Json),
        new("size.json", PacketFileFormat.Json),
        new("questions.json", PacketFileFormat.Json),
        new("answers.jsonl", PacketFileFormat.JsonLines),
        new("challenge.json", PacketFileFormat.Json),
        new("gap-search.json", PacketFileFormat.Json),
        new("graph.json", PacketFileFormat.Json),
        new("rounds.jsonl", PacketFileFormat.JsonLines),
        new("runs.json", PacketFileFormat.Json),
        new("sources.json", PacketFileFormat.Json),
        new("brief.md", PacketFileFormat.Text),
    ]);

    /// <summary>
    /// The design packet (line spec §3.2 row 2): the spec and its tables. Like every line kind it lists 'packet.json'
    /// (whose 'links' name each file) and 'index.json' (its kind, parents and acceptance chain).
    /// </summary>
    public static PacketKind Design { get; } = new("design",
    [
        Manifest,
        new("spec.md", PacketFileFormat.Text, "spec"),
        new("sections.json", PacketFileFormat.Json, "sections"),
        new("coverage.json", PacketFileFormat.Json, "coverage"),
        new("decisions.json", PacketFileFormat.Json, "decisions"),
        new("acceptance.json", PacketFileFormat.Json, "acceptance"),
        new("signals.json", PacketFileFormat.Json, "signals"),
        new("contracts.json", PacketFileFormat.Json, "contracts"),
        new("lens-rounds.jsonl", PacketFileFormat.JsonLines, "lensRounds"),
        new("style.json", PacketFileFormat.Json, "style"),
        new("escapes.json", PacketFileFormat.Json, "escapes"),
        new("runs.json", PacketFileFormat.Json, "runs"),
        Index,
    ]);

    /// <summary>
    /// The plan graph (line spec §3.2 row 3, LN10): PKT's own task plan, 'tasks.json' (the file 'links.tasks' names),
    /// beside the graph, the executability review, the holdout summary and the answer log.
    /// </summary>
    public static PacketKind Plan { get; } = new("plan",
    [
        Manifest,
        new("tasks.json", PacketFileFormat.Json, "tasks"),
        new("graph.json", PacketFileFormat.Json, "graph"),
        new("exec-review.json", PacketFileFormat.Json, "execReview"),
        new("holdout.json", PacketFileFormat.Json, "holdout"),
        new("answers.jsonl", PacketFileFormat.JsonLines, "answers"),
        Index,
    ]);

    /// <summary>The phase packet (line spec §3.2 row 4): one phase's contract; an amended one adds 'amends' to phase.json.</summary>
    public static PacketKind Phase { get; } = new("phase",
    [
        Manifest,
        new("phase.json", PacketFileFormat.Json, "phase"),
        new("acceptance.json", PacketFileFormat.Json, "acceptance"),
        new("signals.json", PacketFileFormat.Json, "signals"),
        new("holdout.json", PacketFileFormat.Json, "holdout"),
        new("design.md", PacketFileFormat.Text, "design"),
        Index,
    ]);

    /// <summary>The proof pack (line spec §3.2 row 5): its artifacts live under 'app-run/' and 'evidence/', listed one by one in packet.json.</summary>
    public static PacketKind Proof { get; } = new("proof",
    [
        Manifest,
        new("proof.json", PacketFileFormat.Json, "proof"),
        new("red.json", PacketFileFormat.Json, "red"),
        new("before-after.json", PacketFileFormat.Json, "beforeAfter"),
        new("holdout.json", PacketFileFormat.Json, "holdout"),
        new("not-proven.md", PacketFileFormat.Text, "notProven"),
        new("acceptance.json", PacketFileFormat.Json, "acceptance"),
        Index,
    ])
    {
        Directories = [new("app-run", "appRun"), new("evidence", "evidence")],
    };

    /// <summary>The review result (line spec §3.2 row 6).</summary>
    public static PacketKind Review { get; } = new("review",
    [
        Manifest,
        new("review.json", PacketFileFormat.Json, "review"),
        Index,
    ]);

    /// <summary>The PR hand-off (line spec §3.2 row 7).</summary>
    public static PacketKind Handoff { get; } = new("handoff",
    [
        Manifest,
        new("pr.json", PacketFileFormat.Json, "pr"),
        new("body.md", PacketFileFormat.Text, "body"),
        new("green.json", PacketFileFormat.Json, "green"),
        Index,
    ]);

    private static PacketFileSpec Manifest => new(PacketKind.ManifestFile, PacketFileFormat.Json);

    private static PacketFileSpec Index => new("index.json", PacketFileFormat.Json, "index");

    /// <summary>Every kind, in the order help lists them.</summary>
    public static IReadOnlyList<PacketKind> All { get; } = [Build, Scoping, Design, Plan, Phase, Proof, Review, Handoff];

    /// <summary>Finds a kind by its name (ordinal, lower-case).</summary>
    /// <param name="name">The name given to '--kind'.</param>
    /// <returns>The kind, or null when none has that name.</returns>
    public static PacketKind? Find(string name) => All.FirstOrDefault(k => string.Equals(k.Name, name, StringComparison.Ordinal));
}
