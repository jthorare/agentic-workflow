using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.SetupSources;

/// <summary>The serve step's fact (first-run spec §3.2, A5): stale only when both stamps are known and differ, with each build's 'build-info' when it reads.</summary>
public sealed class ServeFactTests
{
    private static readonly BuildInfo _running = new("80f69df0123456789", KnownInstants.Epoch);
    private static readonly BuildInfo _installed = new("7c81646abcdef0123", KnownInstants.Epoch.AddHours(12));

    private static CancellationToken Token => SetupFactsRig.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task A_dev_run_with_no_stamps_is_current()
    {
        await using var rig = new SetupFactsRig();

        var fact = (await rig.Facts.Serve(Token)).Value;

        Assert.Equal((true, null, null), (fact.Current, fact.Running, fact.Installed));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_same_stamp_is_current_and_carries_its_build()
    {
        await using var rig = new SetupFactsRig();
        rig.Builds.Runs = ("20261122-1031", _installed);
        rig.Builds.Links = ("20261122-1031", _installed);

        var fact = (await rig.Facts.Serve(Token)).Value;

        Assert.Equal((true, _installed), (fact.Current, fact.Running));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Different_stamps_are_stale_with_both_builds()
    {
        await using var rig = new SetupFactsRig();
        rig.Builds.Runs = ("20261121-2210", _running);
        rig.Builds.Links = ("20261122-1031", _installed);

        var fact = (await rig.Facts.Serve(Token)).Value;

        Assert.Equal((false, _running, _installed), (fact.Current, fact.Running, fact.Installed));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pruned_running_folder_is_stale_without_its_build()
    {
        await using var rig = new SetupFactsRig();
        rig.Builds.Runs = ("20261120-0900", null);
        rig.Builds.Links = ("20261122-1031", _installed);

        var fact = (await rig.Facts.Serve(Token)).Value;

        Assert.Equal((false, null), (fact.Current, fact.Running));
    }
}
