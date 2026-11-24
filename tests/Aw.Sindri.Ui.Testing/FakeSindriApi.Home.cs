using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The Home read of the fake (home spec §8): the first page and one answer per cursor, from the server's goldens.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<string?> _homeReads = [];

    /// <summary>GET /sindri/home's answer for the first page.</summary>
    public RouteAnswer HomeFirstPage { get; set; } = RouteAnswer.Api("home", "fixture");

    /// <summary>GET /sindri/home?after='s answer per cursor (a cursor without one gets the empty page).</summary>
    public Dictionary<string, RouteAnswer> HomePagesAfter { get; } = new(StringComparer.Ordinal);

    /// <summary>The cursor of every Home read received, in order: null for a first page.</summary>
    public IReadOnlyList<string?> HomeReads { get { lock (_gate) { return [.. _homeReads]; } } }

    public Task<Result<HomeView>> Home(Uri api, string? after, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _homeReads.Add(after);
        }

        var answer = after is null ? HomeFirstPage : HomePagesAfter.GetValueOrDefault(after, RouteAnswer.Api("home", "empty"));
        return View<HomeView>(api, SindriApiClient.HomePath, answer, sequence: null, gate: null, cancellationToken);
    }
}
