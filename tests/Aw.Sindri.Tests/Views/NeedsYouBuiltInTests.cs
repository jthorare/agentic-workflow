using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Tests.Views;

/// <summary>A built-in service's failure row: it offers RunService only (a built-in can't be paused) and outlives a profile check that does not name it.</summary>
public sealed class NeedsYouBuiltInTests
{
    private static readonly ServiceName _gaps = ServiceName.From("sync-gaps");
    private static readonly ServiceName _heartbeat = ServiceName.From("heartbeat");
    private static readonly ProfileHash _hash = ProfileHash.From(new string('a', 64));
    private readonly ViewsHarness _v = new(new BuiltInService(_gaps, new Cadence(TimeSpan.FromMinutes(1))));

    private SliceHarness H => _v.Slice;

    [Fact, Trait("Lane", "unit")]
    public async Task A_built_in_failure_row_offers_run_only_and_a_profile_services_row_offers_both()
    {
        await Ran(_gaps, ServiceOutcome.Failed);
        await Ran(_heartbeat, ServiceOutcome.Failed);

        var rows = await Rows();

        Assert.Equal(["RunService"], rows["failure:service:sync-gaps"].Actions.Select(a => a.Value), StringComparer.Ordinal);
        Assert.Equal(("RunService", "sindri services run sync-gaps"), (rows["failure:service:sync-gaps"].PrimaryAction!.Value.Value, rows["failure:service:sync-gaps"].Cli));
        Assert.Equal(["RunService", "PauseService"], rows["failure:service:heartbeat"].Actions.Select(a => a.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_built_in_failure_row_stands_at_a_profile_check_until_a_run_clears_it()
    {
        await Ran(_gaps, ServiceOutcome.Failed);
        await Ran(_heartbeat, ServiceOutcome.Failed);

        await _v.Fold(EventTypes.ProfileChecked, new ProfileChecked(H.Ids.New<CommandId>(), _hash, _hash, Drifted: false, [], [ServiceName.From("index")]));

        Assert.Equal(["failure:service:sync-gaps"], (await Rows()).Keys, StringComparer.Ordinal);   // heartbeat is dropped: the profile no longer names it
        await Ran(_gaps, ServiceOutcome.Ok);
        Assert.Empty(await Rows());
    }

    private async Task<Dictionary<string, NeedsYouRow>> Rows() =>
        (await _v.Views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, TestContext.Current.CancellationToken)).Value.ToDictionary(r => r.Row.Id.Value, r => r.Row, StringComparer.Ordinal);

    private Task Ran(ServiceName service, ServiceOutcome outcome) =>
        _v.Fold(EventTypes.ServiceRan, new ServiceRan(H.Ids.New<CommandId>(), service, H.Clock.GetUtcNow(), outcome, outcome == ServiceOutcome.Failed ? "SND-SCHED-001" : null));
}
