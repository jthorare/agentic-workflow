using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>The IStaticSite contract: a folder that exists is served on a loopback origin (two sites on two), one that does not is AW-EVIDENCE-015.</summary>
public abstract class StaticSiteContract
{
    /// <summary>Arranges the port and a folder that exists.</summary>
    /// <returns>The port and the folder.</returns>
    protected abstract Task<(IStaticSite Site, string Folder)> ArrangeAsync();

    /// <summary>Arranges the port and a folder that does not exist.</summary>
    /// <returns>The port and the missing folder.</returns>
    protected abstract Task<(IStaticSite Site, string Folder)> ArrangeMissingAsync();

    /// <summary>Two sites at once answer on two loopback origins.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Two_sites_answer_on_two_loopback_origins()
    {
        var (site, folder) = await ArrangeAsync();

        var first = await site.ServeAsync(folder, CancellationToken.None);
        var second = await site.ServeAsync(folder, CancellationToken.None);

        Expect.That(first.IsSuccess && second.IsSuccess, "both start");
        Expect.That(first.Value.Origin.StartsWith("http://127.0.0.1:", StringComparison.Ordinal) && first.Value.Origin != second.Value.Origin, "on two loopback origins");
        await first.Value.DisposeAsync();
        await second.Value.DisposeAsync();
    }

    /// <summary>A folder that is not there is a run failure.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_folder_is_a_run_failure()
    {
        var (site, folder) = await ArrangeMissingAsync();

        var served = await site.ServeAsync(folder, CancellationToken.None);

        Expect.That(served.IsError && served.Error.Code == EvidenceErrors.RunFailed.Code, "a missing folder is AW-EVIDENCE-015");
    }
}
