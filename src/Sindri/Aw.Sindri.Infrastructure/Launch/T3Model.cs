namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>A T3 provider instance and one of its models.</summary>
/// <param name="InstanceId">The provider instance ('claudeAgent').</param>
/// <param name="Model">The model ('claude-sonnet-5-5').</param>
internal sealed record T3Model(string InstanceId, string Model);
