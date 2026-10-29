using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>
/// Check again (sandbox spec §6, §8): the box probe, the form check, the login probe and a throw-away self-check on a fixture plan, every blocker on one report so the first
/// run is one visit, not four. When nothing blocks, it lists the parked instances it would resume: oldest first, no more than the free session slots.
/// </summary>
/// <param name="sandbox">The mechanism.</param>
/// <param name="forms">The form check.</param>
/// <param name="logins">The login probe.</param>
/// <param name="fixture">The fixture request.</param>
/// <param name="fixtureLogin">The login the fixture requests carry.</param>
/// <param name="resume">The parked instances and the free slots.</param>
internal sealed class SandboxChecks(ISandbox sandbox, ISandboxFormCheck forms, ISandboxLoginCheck logins, ISandboxFixture fixture, ISandboxFixtureLogin fixtureLogin, SandboxResumeList resume) : ISandboxChecks
{
    public async ValueTask<SandboxCheckReport> Run(CancellationToken cancellationToken)
    {
        var blockers = new List<SandboxBlocker>();
        var probe = sandbox.Probe();
        if (probe.IsError)
        {
            blockers.Add(SandboxBlocker.Of(probe.Error));
        }

        blockers.AddRange(await forms.Check(cancellationToken).ConfigureAwait(false));
        blockers.AddRange(await logins.Check(cancellationToken).ConfigureAwait(false));
        var hooksSkipped = false;
        if (probe.IsSuccess)
        {
            var (failed, firstWithoutLogin) = await SelfCheck(fixture.Request(), cancellationToken).ConfigureAwait(false);
            hooksSkipped |= firstWithoutLogin;
            if (failed is not null)
            {
                blockers.Add(failed);
            }
            else
            {
                var (writeFailed, writeWithoutLogin) = await SelfCheck(fixture.WriteScopeRequest(), cancellationToken).ConfigureAwait(false);   // the git canary only runs for a writable work tree, so the ReadOnly fixture never reaches it
                hooksSkipped |= writeWithoutLogin;
                if (writeFailed is not null)
                {
                    blockers.Add(writeFailed);
                }
            }
        }

        IReadOnlyList<string> notChecked = hooksSkipped ? [SandboxCheckReport.HooksNotChecked] : [];
        if (blockers.Count > 0)
        {
            return new SandboxCheckReport(blockers, []) { NotChecked = notChecked };
        }

        var next = await resume.NextParked(cancellationToken).ConfigureAwait(false);
        return new SandboxCheckReport(blockers, [.. next.Select(static p => p.Instance)]) { Resume = next, NotChecked = notChecked };
    }

    /// <summary>Plans a fixture and proves the plan inside the sandbox; the first failure is the blocker. No fixture on this box skips it; a fixture planned with no login has no agent home, so the hooks canary is skipped and the outcome says so.</summary>
    private async ValueTask<(SandboxBlocker? Blocker, bool PlannedWithoutLogin)> SelfCheck(SandboxRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return (null, false);
        }

        var fixtureLoginPrepared = await fixtureLogin.Prepare(request.Launch, cancellationToken).ConfigureAwait(false);
        try
        {
            var planned = fixtureLoginPrepared is { } l ? request with { Target = l.Target, Settings = l.Settings, Login = l.Login } : request;
            var plan = sandbox.Plan(planned);
            if (plan.IsError)
            {
                return (SandboxBlocker.Of(plan.Error), fixtureLoginPrepared is null);
            }

            var proved = await sandbox.SelfCheck(plan.Value, cancellationToken).ConfigureAwait(false);
            return (proved.IsError ? SandboxBlocker.Of(proved.Error) : null, fixtureLoginPrepared is null);
        }
        finally
        {
            if (fixtureLoginPrepared is not null)
            {
                fixtureLogin.Release(fixtureLoginPrepared.Login);
            }
        }
    }
}
