using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>The complete route as a launched session calls it (P8 U4, API spec §8.1): the secret travels in a header, not the body.</summary>
[Lane(TestLane.Integration)]
public sealed class AgentReportRouteTests
{
    private const string Proof = "route-test-launch-secret";

    [Fact(Timeout = 60_000)]
    public async Task The_secret_header_reaches_the_service_and_is_never_logged()
    {
        var ct = TestContext.Current.CancellationToken;
        var logs = new ListLoggerFactory();
        var service = new Capture();
        await using var host = await GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml,
            extra: services =>
            {
                _ = services.AddSingleton<ILoggerFactory>(logs);
                _ = services.Replace(ServiceDescriptor.Scoped<IService<CompleteStepRequest, CompleteStepResponse>>(_ => service));
            });
        var request = new CompleteStepRequest("ignored", RunToken.For(1, 1), OutcomeName.From("done"), new Dictionary<FactKey, ReadOnlyMemory<byte>>());
        var body = JsonSerializer.Serialize(request, host.App.Services.GetRequiredService<JsonSerializerOptions>());
        var route = "/sindri/workflow/instances/WFI-" + new string('1', 32) + "/runs/1.1/complete";

        var answer = await host.Http.Ask(Post(route, body, (SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli"), (SindriHeaders.LaunchSecret, Proof)));

        var seen = Assert.Single(service.Seen);
        Assert.Equal(Proof, seen.LaunchSecret?.Value);
        Assert.DoesNotContain(Proof, body, StringComparison.Ordinal);
        Assert.DoesNotContain(Proof, answer.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(logs.Logger.Lines, static l => l.Contains(Proof, StringComparison.Ordinal));
        var ledger = (await host.App.Services.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, ct)).Value;
        Assert.DoesNotContain(ledger, static e => e.Envelope.Payload.Contains(Proof, StringComparison.Ordinal));
        var without = await host.Http.Ask(Post(route, body, (SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli")));
        Assert.Null(service.Seen[^1].LaunchSecret);
        Assert.NotNull(without);
    }

    private sealed class Capture : IService<CompleteStepRequest, CompleteStepResponse>
    {
        private readonly List<CompleteStepRequest> _seen = [];

        public IReadOnlyList<CompleteStepRequest> Seen => _seen;

        public ValueTask<Result<CompleteStepResponse>> Handle(CompleteStepRequest request, CancellationToken cancellationToken)
        {
            _seen.Add(request);
            return ValueTask.FromResult<Result<CompleteStepResponse>>(SindriErrors.AuthRefused.With("the capture stops the call here"));
        }
    }
}
