using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>The fixture login: the profile's credential source for the Step agent.</summary>
/// <param name="setup">The catalog and what is installed.</param>
/// <param name="catalog">The entries' policy forms.</param>
/// <param name="approved">The approved profile.</param>
/// <param name="sources">The credential sources.</param>
internal sealed class SandboxFixtureLogin(ILaunchSetup setup, IAgentCatalog catalog, IApprovedProfile approved, ModelCredentialSources sources) : ISandboxFixtureLogin
{
    public async ValueTask<FixtureLogin?> Prepare(LaunchId launch, CancellationToken cancellationToken)
    {
        if (await SandboxLoginTarget.Find(setup, catalog, approved, cancellationToken).ConfigureAwait(false) is not var (target, settings))
        {
            return null;
        }

        var source = LoginSourceResolver.Resolve(settings, target, RepoName.From("fixture"), stepAsk: null);
        var prepared = await sources.Prepare(source, target, launch, SandboxLoginTarget.Needed, cancellationToken).ConfigureAwait(false);
        return prepared.IsError ? null : new FixtureLogin(target, settings, prepared.Value);
    }

    public void Release(PreparedLogin login) => sources.Release(login);
}
