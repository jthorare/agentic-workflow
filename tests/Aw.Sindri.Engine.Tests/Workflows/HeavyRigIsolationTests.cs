using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The heavy-slot tests' rig must not read the box (#1173): the Sindri it composes has its own state root, so the
/// box's heavy-slots-cap file (the cap file wins over AW_HEAVY_SLOTS) never gives the tests more than their one slot.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class HeavyRigIsolationTests : IDisposable
{
    private readonly HeavyRig _rig = new();

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task The_rig_s_Sindri_reads_its_own_state_root_and_so_one_slot_whatever_the_box_cap_file_holds()
    {
        await using var sindri = await _rig.Compose(FakeBlockStep.Completing("ui"));
        var boxRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow"));
        var root = sindri.GetRequiredService<SindriSettings>().StateDirectory;

        // the box's own root is where a cap of 4 would sit; the rig's Sindri must not look there
        Assert.NotEqual(boxRoot, root, StringComparer.Ordinal);
        Assert.False(File.Exists(Path.Combine(root, "heavy-slots-cap")));
        Assert.Equal(1, sindri.GetRequiredService<HeavyLockSettings>().Slots);
    }
}
