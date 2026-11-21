namespace Aw.Sindri.Tests.HomeLanes;

/// <summary>The output of the three read-only aw-meta commands, trimmed from a real run on 2026-11-21 (11 lanes; transcript paths and session ids replaced, free text dropped). Keys and casing are as aw-meta prints them.</summary>
internal static class LaneFixtures
{
    /// <summary>'aw-meta status --json' with a three-entry merge queue (one lane has two PRs). obligations and ownerQueue are in the real output too; the collector ignores them.</summary>
    public const string Status = """
        {"obligations":[],"mergeQueue":[{"pr":2897,"from":"sindri-v3-ui","gate":"none","github":null},{"pr":2950,"from":"sindri-v3-ui","gate":"none","github":null},{"pr":3214,"from":"sindri-v3-api","gate":"none","github":"queued 2"}],"ownerQueue":[{"number":7,"askedBy":"ux","text":"x","askedAt":"2026-11-12T16:53:21.9290000Z","answer":null,"answeredAt":null}],"context":[],"lanes":{"entries":[]}}
        """;

    /// <summary>'aw-meta status --json' with an empty merge queue.</summary>
    public const string StatusEmpty = """{"obligations":[],"mergeQueue":[],"ownerQueue":[],"context":[],"lanes":{"entries":[]}}""";

    /// <summary>'aw-meta lanes-context --json' where the lane 'api' is over the mark and the meta lane is under it.</summary>
    public const string ContextRecycle = """
        {"recycleAt":200000,"lanes":[{"lane":"meta","session":"s-meta","transcript":"/scrubbed/meta.jsonl","tokens":146236,"lastActivity":"2026-11-21T21:08:00.5848446Z","threshold":200000,"verdict":"Ok"},{"lane":"api","session":"s-api","transcript":"/scrubbed/api.jsonl","tokens":212004,"lastActivity":"2026-11-21T21:08:01.4058350Z","threshold":200000,"verdict":"Recycle"}],"attention":true}
        """;

    /// <summary>'aw-meta lanes-context --json' where the meta lane itself is over the mark.</summary>
    public const string ContextMetaRecycle = """
        {"recycleAt":200000,"lanes":[{"lane":"meta","session":"s-meta","transcript":"/scrubbed/meta.jsonl","tokens":251000,"lastActivity":"2026-11-21T21:08:00.5848446Z","threshold":200000,"verdict":"Recycle"}],"attention":true}
        """;

    /// <summary>'aw-meta lanes-context --json' with every lane under the mark.</summary>
    public const string ContextOk = """
        {"recycleAt":200000,"lanes":[{"lane":"meta","session":"s-meta","transcript":"/scrubbed/meta.jsonl","tokens":146236,"lastActivity":"2026-11-21T21:08:00.5848446Z","threshold":200000,"verdict":"Ok"}],"attention":false}
        """;

    /// <summary>'aw-meta usage --json' with every subject ok.</summary>
    public const string UsageOk = """
        {"subjects":[{"subject":"api","state":"Ok","agent":"claude-code","model":"sonnet","signalNote":"Claude Code shows only the limit stop"},{"subject":"meta","state":"Ok","agent":"claude-code"}],"attention":false}
        """;

    /// <summary>'aw-meta usage --json' with 'engine' limited for the session window. The kind and resetsAt keys come from aw-meta's UsageRow (no limited subject was on screen when this was captured).</summary>
    public const string UsageLimited = """
        {"subjects":[{"subject":"api","state":"Ok","agent":"claude-code"},{"subject":"engine","state":"Limited","kind":"Session","resetsAt":"2026-11-21T23:00:00+00:00","source":"pane","agent":"claude-code","model":"sonnet"},{"subject":"qa","state":"Unknown","reason":"no signal","agent":"claude-code"}],"attention":true}
        """;
}
