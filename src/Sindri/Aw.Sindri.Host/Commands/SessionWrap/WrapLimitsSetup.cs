using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>The limits of a sandboxed session and the control of its scope.</summary>
/// <param name="Limits">The limits.</param>
/// <param name="Scope">The scope the limits apply to.</param>
internal sealed record WrapLimitsSetup(WrapLimits Limits, IScopeControl Scope);
