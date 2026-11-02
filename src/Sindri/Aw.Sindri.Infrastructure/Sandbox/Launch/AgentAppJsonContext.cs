using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>The source-generated context behind <see cref="GitHubAppTokenSource"/>.</summary>
[JsonSerializable(typeof(InstallationTokenRequestWire))]
[JsonSerializable(typeof(InstallationTokenWire))]
internal sealed partial class AgentAppJsonContext : JsonSerializerContext;
