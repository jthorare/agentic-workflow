using Aw.Meta.Core.Ports;
using Aw.Meta.Features.ShowVersion;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.ShowVersion;

public sealed class ShowVersionTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ShowVersion_reports_the_binary_s_stamp()
    {
        var stamp = new BuildInfo("abc123", "2026-10-17T12:00:00Z", "macOS", "osx-arm64");

        var shown = await new ShowVersionService(new InMemoryBuildInfo { Stamp = stamp }).Handle(new ShowVersionRequest(), CancellationToken.None);

        Assert.True(shown.IsSuccess);
        Assert.Equal(stamp, shown.Value);
    }
}
