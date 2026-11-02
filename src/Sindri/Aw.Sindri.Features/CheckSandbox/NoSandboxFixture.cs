using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>No fixture plan on this box yet: the self-check is skipped, not failed. The switch (SB-5a) replaces it with the real fixture once a mechanism can run one.</summary>
internal sealed class NoSandboxFixture : ISandboxFixture
{
    public SandboxRequest? Request() => null;

    public SandboxRequest? WriteScopeRequest() => null;
}
