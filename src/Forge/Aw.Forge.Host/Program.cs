using Aw.Forge.Host;
using Aw.Forge.Host.Commands;
using Aw.Kernel.Json;
using Aw.Kernel.Telemetry;
using Microsoft.Extensions.DependencyInjection;

ServiceCollection services;
try
{
    services = [];
    _ = Composition.Compose(services, Environment.GetEnvironmentVariable, TimeProvider.System);
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 2;
}

var json = AwJson.CreateOptions(ForgeJsonContext.Default);   // the one Kernel JSON profile
var provider = services.BuildServiceProvider();   // validation runs in HostTests, not at CLI startup
await using (provider.ConfigureAwait(false))
{
    _ = provider.GetRequiredService<TelemetryRuntime>();   // resolved so the container disposes it on exit, which flushes
    return await CommandTable.Run(args, provider, json).ConfigureAwait(false);
}
