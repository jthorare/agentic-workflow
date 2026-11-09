using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Workflows.Spike;
using Temporalio.Api.OperatorService.V1;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The pinned time-skipping test server, once per test class, with Sindri's search attributes registered as 'sindri
/// serve' registers them on the dev server (engine spec rev 8 §5.1). Time moves only when a test calls
/// <see cref="WorkflowEnvironment.DelayAsync(TimeSpan, CancellationToken?)"/>.
/// </summary>
public sealed class SindriTimeSkippingServer : IAsyncLifetime
{
    private WorkflowEnvironment? _environment;

    /// <summary>The started environment.</summary>
    public WorkflowEnvironment Environment => _environment ?? throw new InvalidOperationException("the server has not started");

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        _environment = await TemporalServerFixture.Start(ServerKind.TimeSkipping);
        var attributes = new AddSearchAttributesRequest { Namespace = _environment.Client.Options.Namespace };
        attributes.SearchAttributes.Add(SindriTemporal.SearchAttributes.ToDictionary(static a => a.Name, static a => a.Type, StringComparer.Ordinal));
        _ = await _environment.Client.OperatorService.AddSearchAttributesAsync(attributes);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_environment is not null)
        {
            await _environment.ShutdownAsync();
        }
    }
}
