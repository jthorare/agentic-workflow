namespace Aw.Context.Testing.Fakes;

/// <summary>A request a fake server received. Headers are never recorded, so no credential lands here.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The path and query.</param>
/// <param name="Operation">What the server took the request for (GraphQL root fields, or the route), or the reason it refused it.</param>
/// <param name="Status">The status code it answered with.</param>
public sealed record RecordedRequest(string Method, string Path, string Operation, int Status);
