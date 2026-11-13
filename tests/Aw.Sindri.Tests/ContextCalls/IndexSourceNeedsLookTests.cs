using Aw.Context.Core.Map;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Each context source row carries needsLook, decided on the server (issue #3430): a failing pull or a source that holds nothing and never committed is a look; a running or new pull is not.</summary>
public sealed class IndexSourceNeedsLookTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, SourceProgress> _sources = new(StringComparer.Ordinal)
    {
        ["failed pull"] = Source() with { Pull = "failed", Records = 40, LastCommit = _now.AddHours(-2) },
        ["pull in backoff"] = Source() with { Pull = "backoff", Records = 40, LastCommit = _now.AddHours(-2) },
        ["idle with nothing ever committed"] = Source() with { Pull = "idle", Records = 0 },
        ["idle with records"] = Source() with { Pull = "idle", Records = 40, LastCommit = _now.AddHours(-2) },
        ["idle, empty but committed"] = Source() with { Pull = "idle", Records = 0, LastCommit = _now.AddHours(-2) },
        ["running first read"] = Source() with { Pull = "running", Records = 0 },
        ["a pull word the app never saw"] = Source() with { Pull = "throttled", Records = 0 },
        ["unread pull, unusable with a code"] = Source(usable: false, lastCode: "SND-CONTEXT-010"),
        ["unread pull, unusable with no code"] = Source(usable: false, lastCode: null),
        ["unread pull, usable with a code"] = Source(usable: true, lastCode: "SND-CONTEXT-059"),
    };

    private readonly SliceHarness _h = new();
    private readonly FakeIndexSync _index = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, bool> Cases => new()
    {
        { "failed pull", true },
        { "pull in backoff", true },
        { "idle with nothing ever committed", true },
        { "idle with records", false },
        { "idle, empty but committed", false },
        { "running first read", false },
        { "a pull word the app never saw", false },
        { "unread pull, unusable with a code", true },
        { "unread pull, unusable with no code", false },
        { "unread pull, usable with a code", false },
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Cases))]
    public async Task The_status_row_says_whether_the_source_needs_a_look(string name, bool look)
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile("5m"), Ct);
        _index.Standing = [_sources[name]];
        using var caller = _h.Callers.EnterService("test");

        var view = await new GetIndexStatusService(new ContextScopes(_h.Profiles, _h.Callers), IndexStoreEngineProxy.Create(_index, new MapHealth("context:store", MapHealthState.Ok, "ok", Fix: null))).Handle(new GetIndexStatusRequest(), Ct);

        Assert.True(view.IsSuccess, name);
        Assert.Equal(look, Assert.Single(view.Value.Sources).NeedsLook);
    }

    private static SourceProgress Source(bool usable = true, string? lastCode = null) =>
        new(SourceId.From("linear"), usable ? SyncPhase.Incremental : SyncPhase.Recent, _now.AddMinutes(-5), lastCode, Usable: usable, Complete: usable);
}
