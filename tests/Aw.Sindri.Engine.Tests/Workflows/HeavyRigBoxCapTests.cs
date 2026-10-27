using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A box whose state root holds a heavy-slots-cap of 4 must not give the heavy-slot tests' rig more than its one slot (#1173).
/// The root a Sindri reads without AW_STATE_DIR is ~/.agentic-workflow, found through HOME, so the test points HOME at a temp
/// folder holding that cap file for the length of the rig's composition. It sets a process variable, so it runs alone.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]   // sets HOME for the whole process: no other test runs beside it
public sealed class HeavyRigBoxCapTests : IDisposable
{
    private readonly HeavyRig _rig = new();

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task The_rig_sees_one_slot_when_its_parent_s_state_root_holds_a_cap_of_4()
    {
        var box = Directory.CreateTempSubdirectory("aw-heavy-box-");
        var oldHome = Environment.GetEnvironmentVariable("HOME");
        try
        {
            var boxRoot = Path.Combine(box.FullName, ".agentic-workflow");
            _ = Directory.CreateDirectory(boxRoot);
            await File.WriteAllTextAsync(Path.Combine(boxRoot, "heavy-slots-cap"), "4", TestContext.Current.CancellationToken);
            Environment.SetEnvironmentVariable("HOME", box.FullName);

            await using var sindri = await _rig.Compose(FakeBlockStep.Completing("ui"));

            Assert.NotEqual(boxRoot, sindri.GetRequiredService<SindriSettings>().StateDirectory, StringComparer.Ordinal);
            Assert.Equal(1, sindri.GetRequiredService<HeavyLockSettings>().Slots);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", oldHome);
            box.Delete(recursive: true);
        }
    }
}
