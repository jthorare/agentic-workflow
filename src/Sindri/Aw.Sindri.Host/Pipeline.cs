using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host;

/// <summary>The request pipeline Program.cs runs: the loopback guard, the API's own problems (exceptions, refused input), Vogen's 400 mapping, then the endpoint table.</summary>
internal static class Pipeline
{
    internal static WebApplication Configure(WebApplication app)
        => app.UseLoopbackGuard().UseApiProblems().UseValueObjectValidation().MapSindriEndpoints();
}
