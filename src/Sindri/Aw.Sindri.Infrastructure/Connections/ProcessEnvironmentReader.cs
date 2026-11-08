using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>IEnvironmentReader over this process's environment.</summary>
internal sealed class ProcessEnvironmentReader : IEnvironmentReader
{
    public string? Get(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
}
