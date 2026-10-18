using System.Net;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

/// <summary>A resolver over a fixed table.</summary>
internal sealed class FakeEgressResolver(Dictionary<string, IPAddress[]> table) : IEgressResolver
{
    public ValueTask<IReadOnlyList<IPAddress>> Resolve(string host, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<IPAddress>>(table.GetValueOrDefault(host) ?? []);
}
