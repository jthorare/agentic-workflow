using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IBuildInfo contract: the same tests run against the in-memory fake and the real assembly reader.</summary>
public abstract class BuildInfoContract
{
    /// <summary>The adapter under test.</summary>
    /// <returns>The adapter.</returns>
    protected abstract IBuildInfo Create();

    /// <summary>Every field is filled: a value, or "unknown" for what the build did not stamp.</summary>
    [Fact]
    public void Every_field_has_a_value()
    {
        var build = Create().Read();

        Expect.That(build.Commit.Length > 0 && build.BuiltAt.Length > 0, "the commit and build time are a value or \"unknown\"");
        Expect.That(build.Os.Length > 0 && build.Rid.Length > 0, "the OS and runtime identifier are known");
    }

    /// <summary>Reading twice gives the same stamp.</summary>
    [Fact]
    public void The_stamp_does_not_change_between_reads()
    {
        var adapter = Create();

        Expect.That(adapter.Read() == adapter.Read(), "the same stamp");
    }
}
