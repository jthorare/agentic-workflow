using Aw.Kernel;
using Aw.Sindri.Host.Commands.SessionWrap;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>A limits source that answers one scripted result and remembers the path it was asked for.</summary>
internal sealed class FakeLimitsSource(Result<WrapLimitsSetup> answer) : IWrapLimitsSource
{
    /// <summary>Gets the path of the last open, or null.</summary>
    internal string? Opened { get; private set; }

    public Result<WrapLimitsSetup> Open(string path)
    {
        Opened = path;
        return answer;
    }
}
