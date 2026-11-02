using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The source-generated context behind <see cref="SandboxFilesJson"/>.</summary>
[JsonSerializable(typeof(Core.Sandbox.Plan.SandboxPlan))]
[JsonSerializable(typeof(Core.Control.ControlEndpoint))]
[JsonSerializable(typeof(WrapLimitsFile))]
internal sealed partial class SandboxFilesJsonContext : JsonSerializerContext;
