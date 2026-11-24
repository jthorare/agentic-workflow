using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IInstalledBuilds contract, run against the in-memory fake and the file adapter.</summary>
public abstract class InstalledBuildsContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh reader plus a way to record a binary: its name, revision and input pathspecs (null records no pathspecs).</summary>
    /// <returns>The reader and the recorder.</returns>
    protected abstract (IInstalledBuilds Builds, Action<string, string, IReadOnlyList<string>?> Put) Create();

    /// <summary>A binary the build holds no record of reads as null.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_binary_with_no_record_reads_as_null()
    {
        var (builds, _) = Create();

        var read = await builds.Current("judge", _none);

        Expect.That(read.IsSuccess && read.Value is null, "null");
    }

    /// <summary>A recorded binary reads its revision and its pathspecs in order.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_recorded_binary_reads_its_revision_and_pathspecs_in_order()
    {
        var (builds, put) = Create();
        put("judge", "0123456789abcdef", ["global.json", "src/Judge/Aw.Judge.Host", "src/Aw.Kernel"]);

        var read = await builds.Current("judge", _none);

        Expect.That(read.IsSuccess && read.Value?.Revision == "0123456789abcdef", "the revision");
        Expect.That(read.Value?.Inputs is ["global.json", "src/Judge/Aw.Judge.Host", "src/Aw.Kernel"], "the pathspecs in order");
    }

    /// <summary>A build made before the pathspecs record reads null inputs, and another binary's record is not mixed in.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_build_made_before_the_pathspecs_record_reads_null_inputs()
    {
        var (builds, put) = Create();
        put("sindri", "fedcba9876543210-dirty", null);
        put("judge", "0123456789abcdef", ["src/Judge"]);

        var read = await builds.Current("sindri", _none);

        Expect.That(read.IsSuccess && read.Value is { Revision: "fedcba9876543210-dirty", Inputs: null }, "the revision and no inputs");
    }
}
