using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Tests.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.RunService;

/// <summary>The real job runtime with RunService registered the way the host registers it, the approved profile 'tidy' and 'stuck', and the services a test gives it (API P6 unit D).</summary>
internal static class RunServiceRuntime
{
    /// <summary>Opens a runtime that holds the lease and approves a profile naming 'tidy', 'stuck' and 'missing' (a service this build does not have).</summary>
    /// <param name="services">The background services this build has.</param>
    /// <returns>The runtime.</returns>
    public static Task<RealJobRuntime> Open(params IScheduledService[] services) => Open(services, builtIns: [], profile: true);

    /// <summary>Opens a runtime like <see cref="Open(IScheduledService[])"/>, with built-in services, and optionally with no profile entries at all.</summary>
    /// <param name="service">The one service.</param>
    /// <param name="builtIn">A built-in service.</param>
    /// <param name="profile">Whether the approved profile names services.</param>
    /// <returns>The runtime.</returns>
    public static Task<RealJobRuntime> Open(IScheduledService service, BuiltInService builtIn, bool profile) => Open([service], [builtIn], profile);

    private static async Task<RealJobRuntime> Open(IScheduledService[] services, BuiltInService[] builtIns, bool profile)
    {
        var runtime = new RealJobRuntime(scrubber: null, rows: ServiceRows.All, configure: (collection, h) =>
        {
            _ = collection.AddRunService()
                .AddSingleton<IApprovedProfile>(h.Profiles)
                .AddRecordServiceRun();
            foreach (var builtIn in builtIns)
            {
                _ = collection.AddSingleton(builtIn);
            }

            foreach (var service in services)
            {
                _ = collection.AddSingleton(service);
            }
        });
        _ = await runtime.H.Profiles.Save(profile ? SliceHarness.ProfileOf("thor", ("tidy", "5s"), ("stuck", "5s"), ("missing", "5s")) : SliceHarness.ProfileOf("thor"), TestContext.Current.CancellationToken);
        await runtime.Open();
        return runtime;
    }

    /// <summary>The ledger events of <paramref name="type"/>.</summary>
    /// <param name="runtime">The runtime.</param>
    /// <param name="type">The event type.</param>
    /// <returns>The events, in order.</returns>
    public static IReadOnlyList<Core.Ledger.LedgerEvent> Of(this RealJobRuntime runtime, string type) =>
        [.. runtime.Events.Where(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal))];
}
