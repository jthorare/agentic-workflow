using Aw.Sindri.Host.Server;

namespace Aw.Sindri.Tests.Server;

/// <summary>The exit code as a field, so a test never sets the test host's own.</summary>
internal sealed class FakeExitCode : IExitCode
{
    public int Code { get; private set; }

    public void Set(int code) => Code = code;
}
