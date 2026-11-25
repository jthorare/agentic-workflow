using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.BuildToolkit;

/// <summary>Builds a toolkit as a job (agent-first PR 16): `dotnet build -warnaserror`, then its tests, under a heavy slot. The source hash is recorded only when both pass.</summary>
/// <param name="Name">The toolkit name.</param>
public sealed record BuildToolkitRequest(string Name) : IJob<BuildToolkitResult>;
