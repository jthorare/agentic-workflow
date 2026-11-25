using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Ledger;

public sealed class TempHomeTests
{
    [Fact, Trait("Lane", "unit")]
    public void Dispose_removes_the_sessions_sibling_a_launch_made()
    {
        var home = new TempHome();
        var launch = LaunchId.From("LCH-" + new string('f', 32));
        var sessionHome = SessionHomePaths.For(home.Path, launch);
        _ = Directory.CreateDirectory(sessionHome);

        home.Dispose();

        Assert.False(Directory.Exists(home.Path));
        Assert.False(Directory.Exists(SessionHomePaths.Sessions(home.Path)));
    }
}
