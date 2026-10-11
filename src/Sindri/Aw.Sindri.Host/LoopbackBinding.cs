using System.Globalization;
using System.Net;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Telemetry;
using Aw.Sindri.Host.Server;

namespace Aw.Sindri.Host;

/// <summary>
/// Builds the web host so that the only listener is 127.0.0.1 (spec §4d runtime hardening): no command-line
/// arguments and no environment variables reach the web host's configuration, PreferHostingUrls is off, and Kestrel
/// listens with Listen(IPAddress.Loopback, port) only. The port is AW_SINDRI_PORT (0 picks a free one); serve's host
/// instead adds its one loopback listener through <see cref="ServePort"/> (the variable, else the approved api.port, else
/// 7410) before it starts, and refuses to start when that didn't run (<see cref="ServeListenerCheck"/>).
/// </summary>
internal static class LoopbackBinding
{
    internal const string PortVariable = "AW_SINDRI_PORT";

    internal static WebApplication Build(string[] args, Func<string, string?> environment, TimeProvider clock, Action<IServiceCollection>? configure = null)
    {
        if (args.Length > 0)
        {
            throw new ArgumentException("This host takes no command-line arguments: its address is fixed to loopback.", nameof(args));
        }

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [] });
        builder.Configuration.Sources.Clear();   // drops the ASPNETCORE_/DOTNET_ environment and command-line sources
        _ = builder.Configuration.AddInMemoryCollection();   // the host still writes its own settings somewhere
        _ = builder.WebHost.PreferHostingUrls(preferHostingUrls: false);
        _ = builder.WebHost.UseKestrel(kestrel =>
        {
            if (kestrel.ApplicationServices.GetService<ServePort>() is null)   // serve adds its loopback listener once it chose the port
            {
                kestrel.Listen(IPAddress.Loopback, PortFrom(environment));
            }
        });
        _ = builder.Logging.ClearProviders();   // Kernel telemetry (AddKernelTelemetry in Compose) is the only logger: scrubbed JSON lines
        _ = Composition.Compose(builder.Services, environment, clock);
        configure?.Invoke(builder.Services);
        _ = builder.Services.ConfigureHttpJsonOptions(o => CopyProfile(o.SerializerOptions));
        _ = builder.Services.AddProblemDetails();   // its post-configure step adds ProblemDetails' JSON context to the chain
        var app = builder.Build();
        _ = app.Services.GetRequiredService<TelemetryRuntime>();   // resolved here, so the container disposes it with the app (Program.cs and every test)
        return app;
    }

    private static int PortFrom(Func<string, string?> environment)
        => int.TryParse(environment(PortVariable), NumberStyles.None, CultureInfo.InvariantCulture, out var port) ? port : 0;

    /// <summary>Copies the Kernel JSON profile into ASP.NET's options (AwJson's options are read-only and can't be assigned).</summary>
    private static void CopyProfile(JsonSerializerOptions target)
    {
        var profile = AwJson.CreateOptions(SindriJsonContext.Default);
        target.TypeInfoResolverChain.Clear();   // AddProblemDetails' post-configure step adds ProblemDetails' context after this runs
        target.TypeInfoResolverChain.Add(SindriJsonContext.Default);
        target.PropertyNamingPolicy = profile.PropertyNamingPolicy;
        target.PropertyNameCaseInsensitive = profile.PropertyNameCaseInsensitive;   // the Web defaults are case-insensitive
        target.NumberHandling = profile.NumberHandling;                             // and read numbers from strings
        target.DefaultIgnoreCondition = profile.DefaultIgnoreCondition;
        target.UnmappedMemberHandling = profile.UnmappedMemberHandling;
        target.Encoder = profile.Encoder;
        target.RespectNullableAnnotations = profile.RespectNullableAnnotations;
        target.RespectRequiredConstructorParameters = profile.RespectRequiredConstructorParameters;
        foreach (var converter in profile.Converters)
        {
            target.Converters.Add(converter);
        }
    }
}
