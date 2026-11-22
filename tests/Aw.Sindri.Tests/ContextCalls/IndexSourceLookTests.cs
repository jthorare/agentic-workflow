using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An idle source that holds nothing needs a look only once a pass has ended and still found nothing (issue #3430, issue #3183): a fresh home's sources are not a look before their first pass.</summary>
public sealed class IndexSourceLookTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, SourceProgress> _sources = new(StringComparer.Ordinal)
    {
        ["idle, no pass ended yet"] = Idle(lastSync: null, records: 0, lastCommit: null),
        ["idle, a pass ended and found nothing"] = Idle(_now.AddMinutes(-5), records: 0, lastCommit: null),
        ["idle, a pass ended and committed an empty source"] = Idle(_now.AddMinutes(-5), records: 0, _now.AddMinutes(-5)),
        ["idle, a pass ended with records"] = Idle(_now.AddMinutes(-5), records: 40, _now.AddMinutes(-5)),
        ["idle, no pass ended yet, records from an earlier run"] = Idle(lastSync: null, records: 40, _now.AddDays(-1)),
    };

    public static TheoryData<string, bool> Cases => new()
    {
        { "idle, no pass ended yet", false },
        { "idle, a pass ended and found nothing", true },
        { "idle, a pass ended and committed an empty source", false },
        { "idle, a pass ended with records", false },
        { "idle, no pass ended yet, records from an earlier run", false },
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Cases))]
    public void An_idle_source_with_nothing_needs_a_look_only_after_a_pass_ended(string name, bool look) =>
        Assert.Equal(look, IndexSourceLook.For(_sources[name]));

    private static SourceProgress Idle(DateTimeOffset? lastSync, int records, DateTimeOffset? lastCommit) =>
        new(SourceId.From("library"), SyncPhase.Incremental, lastSync, LastCode: null, Usable: true, Complete: true) { Pull = "idle", Records = records, LastCommit = lastCommit };
}
