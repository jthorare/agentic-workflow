using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox.Credentials;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>A login prepared for a fixture request, with the target and sandbox section it was prepared for.</summary>
/// <param name="Target">The agent.</param>
/// <param name="Settings">The profile's sandbox section.</param>
/// <param name="Login">The prepared login.</param>
internal sealed record FixtureLogin(AgentId Target, SandboxSettings? Settings, PreparedLogin Login);
