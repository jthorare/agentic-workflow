using Aw.Kernel;
using Aw.Sindri.Host.Commands.SessionWrap;

namespace Aw.Sindri.Tests.SessionWrap;

internal sealed class FakeChildren : IChildLauncher
{
    public FakeChild? Child { get; private set; }

    public IReadOnlyList<string>? Argv { get; private set; }

    public IReadOnlyDictionary<string, string>? Environment { get; private set; }

    public Result<IWrapChild> Start(IReadOnlyList<string> argv, IReadOnlyDictionary<string, string> environment)
    {
        Argv = argv;
        Environment = environment;
        Child = new FakeChild();
        return Child;
    }
}
