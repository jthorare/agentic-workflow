using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>Runs the checks and keeps the report as the newest one, whoever asked (Check again, doctor): so the status read has something to say.</summary>
/// <param name="inner">The checks.</param>
/// <param name="last">Where the newest report is kept.</param>
/// <param name="clock">Stamps the report.</param>
internal sealed class RecordingSandboxChecks(ISandboxChecks inner, LastSandboxCheck last, TimeProvider clock) : ISandboxChecks
{
    public async ValueTask<SandboxCheckReport> Run(CancellationToken cancellationToken)
    {
        var report = await inner.Run(cancellationToken).ConfigureAwait(false);
        last.Record(new SandboxCheckRecord(clock.GetUtcNow(), report));
        return report;
    }
}
