using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>Runs the sandbox checks and words their blockers (sandbox spec §6, §8). Read-only: it resumes nothing.</summary>
/// <param name="checks">The checks.</param>
/// <param name="last">The newest check, for when it ran.</param>
internal sealed class CheckSandboxService(ISandboxChecks checks, LastSandboxCheck last) : IService<CheckSandboxRequest, CheckSandboxResponse>
{
    public async ValueTask<Result<CheckSandboxResponse>> Handle(CheckSandboxRequest request, CancellationToken cancellationToken)
    {
        var report = await checks.Run(cancellationToken).ConfigureAwait(false);
        return new CheckSandboxResponse([.. report.Blockers.Select(SandboxBlockerLine.From)], report.ReadyToResume, report.Ready)
        {
            CheckedAt = last.Latest?.At,
            Resume = [.. report.Resume.Select(static p => new SandboxResumeLine(p.Instance, p.Item, p.Title, p.Since))],
        };
    }
}
