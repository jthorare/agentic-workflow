using System.Text.Json;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Linear;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.LinearIssues;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.LinearReads;
using static Aw.Sindri.Tests.LinearIssues.LinearIssueRows;

namespace Aw.Sindri.Tests.LinearWrites;

/// <summary>
/// A Linear write is recorded as one ledger event under the caller's actor (agent-first PR 2, #2258): a launched session's comment and create name that session, the owner's name the owner, and a write nobody
/// confirmed claims nothing. The sessions-off and unmapped-repo refusals are in <see cref="ContextLinearWriteGateTests"/>; these rows run the real services and the real recorder over the in-memory ledger.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class LinearWriteAttributionTests
{
    private const string ClientId = "5d0a4f6e-3b1c-4c0e-9a57-2f1d8c9b7e01";
    private const string Launch = "LCH-00000000000000000000000000000007";

    private readonly SliceHarness _slice = new();
    private readonly InMemoryProfiles _profiles = new();
    private readonly FakeContextLinearWrites _writes = new();
    private readonly FakeLinearWriteGate _gate = new();
    private readonly FakeLinearSideFacts _side = new FakeLinearSideFacts().WithTeam(new LinearLookupsResponse(new LinearTeamRef(EngId, "ENG", "Engineering"), SyncedAt: null, [], [], [], []));
    private readonly FakeLinearIssueFacts _facts = new FakeLinearIssueFacts().WithSource(Connected(Viewer)).WithDetails(new LinearIssueDetailFacts(Row("ENG-12", Viewer), "d", Removed: false, []));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LinearScopes Scopes()
    {
        var profile = new SindriProfile(1, "thor", Repos: [new RepoEntry("web", "/work/web")], Context: JsonDocument.Parse("{}").RootElement.Clone());
        _profiles.Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
        _ = _profiles.Save(_profiles.Live.Value, Token).AsTask().GetAwaiter().GetResult();
        return new LinearScopes(_profiles, _slice.Callers);
    }

    private ILinearWriteRecorder Recorder() => new LedgerLinearWriteRecorder(_slice.Store, _slice.Held, _slice.Events, _slice.Callers);

    private CommentLinearIssueService Comments() => new(Scopes(), _writes, new LinearTeamGate(_side), new GetLinearIssueService(_facts, new FakeLinearItemLinks(), _gate), _slice.Clock, Recorder());

    private CreateLinearIssueService Creates() => new(Scopes(), _writes, new LinearTeamGate(_side), _facts, new GetLinearIssueService(_facts, new FakeLinearItemLinks(), _gate), Recorder());

    [Fact]
    public async Task A_launched_sessions_comment_is_recorded_once_under_that_sessions_actor()
    {
        _ = await _slice.HoldLease();
        var caller = A.Caller().Launched(Launch).InRepo("web").Build();
        using var entered = _slice.Callers.Enter(caller);
        _writes.SetComment(ExternalId.From(ClientId), LinearIssueKey.Parse("ENG-12").Value);
        _writes.Next(new LinearWriteApplied(LiveIssue()));

        var result = await Comments().Handle(new CommentLinearIssueRequest("ENG-12", ClientId, "On it."), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_slice.Store.Events);
        Expect.Equal(("linear.comment.posted", $"linear.comment.posted:{ClientId}"), (recorded.Envelope.Type, recorded.IdempotencyKey));
        Expect.Equal(caller.Actor, recorded.Envelope.Actor);   // why: CallerContext composes the actor; the launch id is in it once the attribution fix (session:launch:<id>) is merged
        Expect.True(recorded.Envelope.Actor.StartsWith("session:", StringComparison.Ordinal));
        Expect.Equal(("ENG", "ENG-12", ClientId), PayloadOf(recorded));
    }

    [Fact]
    public async Task The_owners_create_is_recorded_under_the_owners_actor_with_the_new_issues_key()
    {
        _ = await _slice.HoldLease();
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _writes.Next(new LinearWriteApplied(LiveIssue()));

        var result = await Creates().Handle(new CreateLinearIssueRequest(ClientId, "ENG", "Document the retry budget"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_slice.Store.Events);
        Expect.Equal(("linear.issue.created", $"linear.issue.created:{ClientId}"), (recorded.Envelope.Type, recorded.IdempotencyKey));
        Expect.True(recorded.Envelope.Actor.StartsWith("human:", StringComparison.Ordinal));
        Expect.Equal(("ENG", "ENG-12", ClientId), PayloadOf(recorded));
    }

    [Fact]
    public async Task A_comment_nobody_confirmed_records_nothing()
    {
        _ = await _slice.HoldLease();
        using var entered = _slice.Callers.Enter(A.Caller().Launched(Launch).InRepo("web").Build());
        _writes.Next(new LinearWriteUnknown());

        var result = await Comments().Handle(new CommentLinearIssueRequest("ENG-12", ClientId, "On it."), Token);

        Expect.Equal("SND-LINEAR-011", result.Error.Code);
        Assert.Empty(_slice.Store.Events);
    }

    [Fact]
    public async Task A_session_whose_repo_the_profile_does_not_map_writes_nothing_and_records_nothing()
    {
        _ = await _slice.HoldLease();
        using var entered = _slice.Callers.Enter(A.Caller().Launched(Launch).InRepo("elsewhere").Build());

        var result = await Comments().Handle(new CommentLinearIssueRequest("ENG-12", ClientId, "On it."), Token);

        Expect.Equal("SND-CONTEXT-003", result.Error.Code);
        Assert.Empty(_writes.Calls);
        Assert.Empty(_slice.Store.Events);
    }

    private (string Team, string? Issue, string? ClientId) PayloadOf(Aw.Sindri.Core.Ledger.LedgerEvent recorded)
    {
        var payload = _slice.Events.Payload<LinearWriteRecorded>(recorded).Value;
        return (payload.Team, payload.Issue, payload.ClientId);
    }

    private static LinearIssueView LiveIssue() => new(
        ExternalId.From("00000000-0000-4000-8000-00000000010c"), LinearIssueKey.Parse("ENG-12").Value, "ENG", "Title", Description: null, ExternalId.From("00000000-0000-4000-8000-000000000051"), "Todo",
        AssigneeId: null, Priority: 0, Estimate: null, CycleId: null, ProjectId: null, ParentId: null, [], KnownInstants.Epoch);
}
