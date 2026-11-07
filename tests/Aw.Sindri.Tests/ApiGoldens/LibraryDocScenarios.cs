using Aw.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The library sheet's single-doc read (PR review page spec §6.1, release 1): 'GET /library/{doc}' for a doc the library has and for one it does not (SND-LIB-404). Hand-written goldens.
/// </summary>
internal static class LibraryDocScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [("library-doc", "ok"), ("library-doc", "not-found")];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the host with a library that holds one doc, with one error.</summary>
    public static Task<GoldenHost> Start(CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: static services => _ = services.Replace(ServiceDescriptor.Scoped<ILibraryDocFacts>(static _ => new OneDoc())));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        return (await host.Http.Ask(Get("/sindri/library/" + (scenario == "ok" ? "booking-retry-tdd" : "nope"), ItemScenarios.Human(host)))).Body;
    }

    private sealed class OneDoc : ILibraryDocFacts
    {
        private static readonly LibraryDocView _doc = new(
            "booking-retry-tdd", "aw", "aw/acme-api", "tdd", "draft", Project: null, ["acme/api"], ["412", "ENG-12"], "Booking retry TDD", "~/.agentic-workflow/acme-api/plans/booking-retry-tdd.md",
            [new LibraryDocErrorView("SND-LIB-015", "~/.agentic-workflow/acme-api/plans/booking-retry-tdd.md line 31: queue-sizing-spec#REQ-9 is not in this scope. Fix the ref, or add REQ-9 to the doc it names.")]);

        public ValueTask<Result<LibraryDocView?>> Find(string doc, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success(string.Equals(doc, _doc.Slug, StringComparison.Ordinal) ? _doc : null));
    }
}
