using Aw.Judge.Core.Ports;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>The IExecutableLocator contract: the fake and the PATH adapter both pass it.</summary>
public abstract class ExecutableLocatorContract
{
    /// <summary>A locator for which exactly these executables are installed.</summary>
    /// <param name="installed">The binary names.</param>
    /// <returns>The locator.</returns>
    protected abstract IExecutableLocator Create(IReadOnlyList<string> installed);

    /// <summary>An installed executable is found.</summary>
    [Fact]
    public void An_installed_executable_is_found()
    {
        var locator = Create(["codex", "claude"]);

        Expect.That(locator.IsOnPath("codex") && locator.IsOnPath("claude"), "each installed binary is on the path");
    }

    /// <summary>A name that isn't installed isn't found.</summary>
    [Fact]
    public void A_missing_executable_is_not_found()
    {
        var locator = Create(["codex"]);

        Expect.That(!locator.IsOnPath("cursor-agent"), "a binary that isn't installed is not on the path");
    }
}
