using System.Net;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The policy's answer for one host and port.</summary>
/// <param name="Verdict">The verdict.</param>
/// <param name="Address">The validated address to dial, set when allowed.</param>
internal sealed record EgressDecision(EgressVerdict Verdict, IPAddress? Address);
