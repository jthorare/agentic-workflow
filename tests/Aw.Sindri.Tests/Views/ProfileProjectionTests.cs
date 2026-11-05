using System.Text;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The 'profile' fold (API spec §2.3): approvals set the approved hash, checks the live one and the drift.</summary>
public sealed class ProfileProjectionTests
{
    private static readonly ProfileHash _a = ProfileHash.From(new string('a', 64));
    private static readonly ProfileHash _b = ProfileHash.From(new string('b', 64));
    private static readonly ServiceName _heartbeat = ServiceName.From("heartbeat");
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public void Profile_fold_approve_sets_the_approved_hash_and_when()
    {
        var approved = Approved(_a, 1);

        var row = ProfileProjection.Fold(row: null, new ProfileApproved(_a, []), approved);

        Assert.Equal((_a, approved.Envelope.OccurredAt, false, 1L), (row.Approved, row.ApprovedAt, row.Changed, row.Sequence));
        Assert.Null(row.Live);
        Assert.Empty(row.ChangedPaths);
    }

    [Fact, Trait("Lane", "unit")]
    public void Profile_fold_check_shows_the_live_hash_and_the_changed_paths()
    {
        var row = ProfileProjection.Fold(row: null, new ProfileApproved(_a, []), Approved(_a, 1));
        var check = Checked(_a, _b, drifted: true, ["services.heartbeat.every"], 2);

        row = ProfileProjection.Fold(row, Payload<ProfileChecked>(check), check);

        Assert.Equal((_a, _b, check.Envelope.OccurredAt, true, 2L), (row.Approved, row.Live, row.CheckedAt, row.Changed, row.Sequence));
        Assert.Equal(["services.heartbeat.every"], row.ChangedPaths);
        Assert.Equal([_heartbeat], row.ApprovedServices);
    }

    [Fact, Trait("Lane", "unit")]
    public void Profile_fold_revert_clears_changed()
    {
        var drift = Checked(_a, _b, drifted: true, ["user"], 2);
        var revert = Checked(_a, _a, drifted: false, [], 3);

        var row = ProfileProjection.Fold(ProfileProjection.Fold(row: null, Payload<ProfileChecked>(drift), drift), Payload<ProfileChecked>(revert), revert);

        Assert.Equal((_a, false, 3L), (row.Live, row.Changed, row.Sequence));
        Assert.Empty(row.ChangedPaths);
    }

    [Fact, Trait("Lane", "unit")]
    public void Profile_fold_approving_the_live_profile_clears_changed()
    {
        var drift = Checked(_a, _b, drifted: true, ["user"], 2);
        var approve = Approved(_b, 3);

        var row = ProfileProjection.Fold(ProfileProjection.Fold(row: null, Payload<ProfileChecked>(drift), drift), new ProfileApproved(_b, ["user"]), approve);

        Assert.Equal((_b, _b, false), (row.Approved, row.Live, row.Changed));
        Assert.Empty(row.ChangedPaths);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Replaying_the_same_events_gives_the_same_rows()
    {
        var page = new List<LedgerEvent>
        {
            Approved(_a, 1),
            Recorded.Of(_h.Events, EventTypes.ServiceRan, new ServiceRan(_h.Ids.New<CommandId>(), _heartbeat, _h.Clock.GetUtcNow(), ServiceOutcome.Failed, "SND-SCHED-001"), 2),
            Checked(_a, _b, drifted: true, ["user"], 3),
            Recorded.Of(_h.Events, EventTypes.ServiceRan, new ServiceRan(_h.Ids.New<CommandId>(), _heartbeat, _h.Clock.GetUtcNow().AddSeconds(5), ServiceOutcome.Ok, Code: null), 4),
        };
        IProjection[] projections = [new ServicesProjection(_h.Events), new ProfileProjection(_h.Events)];
        var views = new InMemoryViewStore(SliceHarness.Json);
        _ = await views.Open(ViewSchema.Version, Token);

        _ = await views.Apply(page, projections, _h.Clock.GetUtcNow(), Token);
        var first = await Snapshot(views);
        _ = await views.Reset(ViewNames.All, ViewSchema.Version, _h.Clock.GetUtcNow(), 0, Token);
        _ = await views.Apply(page, projections, _h.Clock.GetUtcNow(), Token);

        Assert.Equal(first, await Snapshot(views));
        Assert.Contains("\"outcome\":\"ok\"", first, StringComparison.Ordinal);
        Assert.Contains("\"changed\":true", first, StringComparison.Ordinal);
    }

    private static async Task<string> Snapshot(InMemoryViewStore views)
    {
        var services = (await views.List<ServiceRow>(ViewNames.Services, Token)).Value;
        var profile = (await views.Get<ProfileRow>(ViewNames.Profile, ProfileProjection.Key, Token)).Value;
        return string.Join('\n', services.Select(Text).Append(Text(profile)));
    }

    private static string Text<TRow>(TRow row) => Encoding.UTF8.GetString(AwJson.Serialize(row, SliceHarness.Json));

    private LedgerEvent Approved(ProfileHash hash, long sequence) => Recorded.Of(_h.Events, EventTypes.ProfileApproved, new ProfileApproved(hash, []), sequence);

    private LedgerEvent Checked(ProfileHash approved, ProfileHash live, bool drifted, IReadOnlyList<string> paths, long sequence) =>
        Recorded.Of(_h.Events, EventTypes.ProfileChecked, new ProfileChecked(_h.Ids.New<CommandId>(), approved, live, drifted, paths, [_heartbeat]), sequence);

    private T Payload<T>(LedgerEvent recorded) => _h.Events.Payload<T>(recorded).Value;
}
