using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class DirectorySessionDirectoryContractTests : SessionDirectoryContract, IDisposable
{
    private readonly TempDirectory _dir = new();

    protected override ISessionDirectory Create(IReadOnlyList<LiveSession> alive, IReadOnlyList<LiveSession> dead)
    {
        foreach (var session in alive.Concat(dead))
        {
            var pid = session.Pid.ToString(CultureInfo.InvariantCulture);
            File.WriteAllText(
                Path.Combine(_dir.Path, pid + ".json"),
                $$"""{"pid":{{pid}},"sessionId":"{{session.Session.Value}}","name":"{{session.Name}}","status":"{{session.Status}}","cwd":"{{session.Cwd}}","kind":"interactive"}""");
        }

        return new DirectorySessionDirectory(_dir.Path, RealLiveness.Create());
    }

    public void Dispose() => _dir.Dispose();
}
