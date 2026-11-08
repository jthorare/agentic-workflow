using Aw.Sindri.Core.Launch;

namespace Aw.Architecture.Tests;

/// <summary>The ids the launcher, sessions and views share live in Core.Launch, outside the engine namespace PR 3 deletes (#2258).</summary>
[Trait("Lane", "integration")]
public sealed class LauncherSharedIdsTests
{
    [Theory]
    [InlineData("LaunchId")]
    [InlineData("RunToken")]
    [InlineData("InstanceAddress")]
    [InlineData("ItemKey")]
    [InlineData("StepRuntime")]
    public void Launcher_shared_ids_live_in_Core_Launch(string name)
    {
        // why: looked up by name so the test compiles before and after the move.
        var type = typeof(StepArgs).Assembly.GetTypes().Single(t => t.Name == name);

        Assert.Equal("Aw.Sindri.Core.Launch", type.Namespace);
    }
}
