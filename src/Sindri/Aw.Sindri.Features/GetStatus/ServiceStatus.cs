using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>A scheduled service.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Every">Its cadence.</param>
/// <param name="LastRun">When it last ran, if ever.</param>
public sealed record ServiceStatus(ServiceName Name, string Every, DateTimeOffset? LastRun);
