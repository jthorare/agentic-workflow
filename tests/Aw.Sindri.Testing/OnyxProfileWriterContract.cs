using Aw.Sindri.Core.Onyx;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IOnyxProfileWriter"/> honours: the first write changes the profile, the same write again changes nothing, another value changes it. Run against the fake and YamlOnyxProfileWriter (which also keeps every other key; its own tests cover that and SND-PROFILE-001/002).</summary>
public abstract class OnyxProfileWriterContract
{
    private const string Url = "http://127.0.0.1:3189";
    private const string Pointer = "keychain:onyx/sindri-onyx";

    /// <summary>A writer over an empty profile.</summary>
    /// <returns>The writer.</returns>
    protected abstract IOnyxProfileWriter Create();

    /// <summary>The first write reports a change; the identical write reports none.</summary>
    [Fact]
    public void The_same_section_twice_changes_nothing_the_second_time()
    {
        var writer = Create();

        Expect.True(writer.SetOnyx(Url, Pointer, "v4.9.0").Value);
        Expect.True(!writer.SetOnyx(Url, Pointer, "v4.9.0").Value);
    }

    /// <summary>A different value is a change.</summary>
    [Fact]
    public void A_different_token_or_version_is_a_change()
    {
        var writer = Create();
        _ = writer.SetOnyx(Url, Pointer, "v4.9.0");

        Expect.True(writer.SetOnyx(Url, "file:/tmp/onyx-key", "v4.9.0").Value);
        Expect.True(writer.SetOnyx(Url, "file:/tmp/onyx-key", "v5.0.0").Value);
    }
}
