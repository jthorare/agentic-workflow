using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>One pinned Temporal server per test class (ExistingPath from binaries.lock; the SDK never downloads).</summary>
public abstract class TemporalServerFixture : IAsyncLifetime
{
    private WorkflowEnvironment? _environment;

    /// <summary>The server this fixture runs.</summary>
    public abstract ServerKind Kind { get; }

    /// <summary>The started environment.</summary>
    public WorkflowEnvironment Environment => _environment ?? throw new InvalidOperationException("the server has not started");

    /// <inheritdoc/>
    public async ValueTask InitializeAsync() => _environment = await Start(Kind);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_environment is not null)
        {
            await _environment.ShutdownAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Starts <paramref name="kind"/> from its pinned binary. The SDK picks each server's port (bind 0, read, release) and the
    /// server then binds it, so a parallel fixture can take it in between ("address already in use"); that start is tried again
    /// with a new port, three times at most.
    /// </summary>
    /// <param name="kind">The server.</param>
    /// <returns>The environment.</returns>
    public static async Task<WorkflowEnvironment> Start(ServerKind kind)
    {
        const int Attempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await StartOnce(kind);
            }
            catch (Exception e) when (attempt < Attempts && IsPortTaken(e))
            {
                await Console.Error.WriteLineAsync(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{kind} server start {attempt} of {Attempts} lost its port ({e.Message.ReplaceLineEndings(" ")}); starting again on another"));
            }
        }
    }

    /// <summary>Whether a failed server start lost its port to another process.</summary>
    /// <param name="e">The failure.</param>
    /// <returns>True for "address already in use".</returns>
    internal static bool IsPortTaken(Exception e) =>
        e.ToString().Contains("address already in use", StringComparison.OrdinalIgnoreCase);

    private static async Task<WorkflowEnvironment> StartOnce(ServerKind kind) => kind switch
    {
        ServerKind.TimeSkipping => await WorkflowEnvironment.StartTimeSkippingAsync(new WorkflowEnvironmentStartTimeSkippingOptions
        {
            TestServer = new TestServerOptions { ExistingPath = PinnedBinaries.Path("temporal-test-server") },
        }),
        ServerKind.Local => await WorkflowEnvironment.StartLocalAsync(new WorkflowEnvironmentStartLocalOptions
        {
            UI = false,
            DevServerOptions = new DevServerOptions { ExistingPath = PinnedBinaries.Path("temporal"), LogLevel = "error" },
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "no such server"),
    };
}
