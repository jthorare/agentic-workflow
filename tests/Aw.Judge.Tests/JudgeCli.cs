using Aw.Judge.Core.Ports;
using Aw.Judge.Host;
using Aw.Judge.Host.Commands;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests;

/// <summary>Runs the judge command line in process, as Program.cs does, on a scratch state directory, capturing its streams.</summary>
/// <param name="state">The AW_STATE_DIR.</param>
public sealed class JudgeCli(TempStateDirectory state)
{
    /// <summary>The clock Compose gets; it starts at the Kernel's test epoch.</summary>
    public FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    /// <summary>Environment variables beyond AW_STATE_DIR and AW_LOG_LEVEL (e.g. AW_SESSION_ID).</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The providers the questions can use, through a FakeProviderCatalog (the agent CLIs among them count as installed); none when null.</summary>
    public IReadOnlyList<IModelProvider>? Providers { get; init; }

    /// <summary>Replaces registrations after Compose (the last registration wins): a recording service in place of a stub, for a routing test.</summary>
    public Action<IServiceCollection>? Overrides { get; init; }

    /// <summary>Runs one command line with empty stdin.</summary>
    public Task<CliRun> Run(params string[] args) => RunWithInput("", args);

    /// <summary>Runs one command line with <paramref name="stdin"/> on stdin.</summary>
    public async Task<CliRun> RunWithInput(string stdin, params string[] args)
    {
        var services = Composition.Compose(
            new ServiceCollection(),
            name => name switch { "AW_STATE_DIR" => state.Path, "AW_LOG_LEVEL" => "none", _ => Environment.GetValueOrDefault(name) },
            Clock);
        // Never the real catalog: no agent CLI or Jev call (nor a keychain read) from a test.
        _ = services.AddSingleton<IProviderCatalog>(new FakeProviderCatalog([.. Providers ?? []]));   // the last registration wins

        Overrides?.Invoke(services);

        var provider = services.BuildServiceProvider();
        await using (provider)
        {
            var capture = CommandOutput.Capture(stdin);
            var code = await CommandTable.Run(args, provider);
            return new CliRun(code, capture.Stdout.ToString(), capture.Stderr.ToString());
        }
    }
}
