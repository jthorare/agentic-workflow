using Aw.Judge.Host;
using Aw.Judge.Host.Commands;
using Aw.Kernel.Telemetry;
using Microsoft.Extensions.DependencyInjection;

var services = Composition.Compose(new ServiceCollection(), Environment.GetEnvironmentVariable, TimeProvider.System);
var provider = services.BuildServiceProvider();   // validation runs in HostTests, not at CLI startup
await using (provider.ConfigureAwait(false))
{
    _ = provider.GetRequiredService<TelemetryRuntime>();   // resolved so the container disposes it on exit, which flushes
    return await CommandTable.Run(args, provider).ConfigureAwait(false);
}
