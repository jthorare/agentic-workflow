using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>Credential_source_contract_holds_for_every_adapter: the fake today, OwnerCopy in SB-2, SeparateLogin in SB-8.</summary>
public sealed class FakeModelCredentialSourceContractTests : IModelCredentialSourceContract
{
    protected override IModelCredentialSource Create(LoginState state) =>
        new FakeModelCredentialSource(ModelLoginSource.OwnerCopy).With(AgentId.From("claude-code"), state);
}
