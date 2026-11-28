using Aw.Kernel.Telemetry.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry.Tests;

/// <summary>#3727: a one-shot client verb never logs or starts a span, so it registers telemetry without starting it; the first thing that needs it starts it, and a problem to report starts it at once.</summary>
[Lane(TestLane.Unit)]
public sealed class DeferredStartTests
{
    [Fact]
    public void A_deferred_registration_starts_nothing_until_a_logger_is_resolved_and_then_logs_and_records_spans()
    {
        var sink = new InMemoryLogSink();
        using var source = new System.Diagnostics.ActivitySource("Aw.Test.Deferred.First");
        var services = new ServiceCollection().AddDeferredKernelTelemetry(new TelemetryOptions { ServiceName = "aw-demo", Sink = sink, SourcePattern = "Aw.Test.Deferred.First" });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.False(provider.GetRequiredService<TelemetryStart>().Started);
        Assert.Null(source.StartActivity("before"));

        provider.GetRequiredService<ILogger<DeferredStartTests>>().Emit(LogLevel.Information, "hello");
        provider.GetRequiredService<TelemetryRuntime>().Flush();

        Assert.True(provider.GetRequiredService<TelemetryStart>().Started);
        Assert.Contains("\"message\":\"hello\"", Assert.Single(sink.Lines), StringComparison.Ordinal);
        using var span = source.StartActivity("after");
        Assert.NotNull(span);
    }

    [Fact]
    public void A_problem_with_the_options_starts_the_runtime_at_once_and_writes_it_once()
    {
        var sink = new InMemoryLogSink();
        var services = new ServiceCollection().AddDeferredKernelTelemetry(
            TelemetryOptions.FromEnvironment("aw-demo", _ => "loud", TimeProvider.System) with { Sink = sink, SourcePattern = "Aw.Test.Deferred.Problem" });

        using var provider = services.BuildServiceProvider();

        Assert.Contains("AW-TELEMETRY-001", Assert.Single(sink.Lines), StringComparison.Ordinal);
        Assert.True(provider.GetRequiredService<TelemetryStart>().Started);
        Assert.Same(provider.GetRequiredService<TelemetryRuntime>(), provider.GetRequiredService<TelemetryRuntime>());
    }

    [Fact]
    public void The_eager_registration_still_starts_at_once()
    {
        var services = new ServiceCollection().AddKernelTelemetry(new TelemetryOptions { ServiceName = "aw-demo", Sink = new InMemoryLogSink(), SourcePattern = "Aw.Test.Deferred.Eager" });

        using var provider = services.BuildServiceProvider();

        Assert.True(provider.GetRequiredService<TelemetryStart>().Started);
    }

    [Fact]
    public void Deferred_and_eager_registration_together_are_refused()
    {
        var services = new ServiceCollection().AddDeferredKernelTelemetry(new TelemetryOptions { ServiceName = "aw-demo", Sink = new InMemoryLogSink(), SourcePattern = "Aw.Test.Deferred.Twice" });

        _ = Assert.Throws<InvalidOperationException>(() => services.AddKernelTelemetry(new TelemetryOptions { ServiceName = "aw-demo", SourcePattern = "Aw.Test.Deferred.Twice" }));
    }

    [Fact]
    public void A_container_that_never_started_the_runtime_disposes_cleanly_and_one_that_did_disposes_it()
    {
        var never = new ServiceCollection().AddDeferredKernelTelemetry(new TelemetryOptions { ServiceName = "aw-demo", Sink = new InMemoryLogSink(), SourcePattern = "Aw.Test.Deferred.Dispose" }).BuildServiceProvider();
        var started = new ServiceCollection().AddDeferredKernelTelemetry(new TelemetryOptions { ServiceName = "aw-demo", Sink = new InMemoryLogSink(), SourcePattern = "Aw.Test.Deferred.Dispose2" }).BuildServiceProvider();
        _ = started.GetRequiredService<TelemetryRuntime>();

        var thrown = Record.Exception(() =>
        {
            never.Dispose();
            started.Dispose();
        });

        Assert.Null(thrown);
    }
}
