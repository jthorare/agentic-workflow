using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.CodeIndexJobs;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the code index's job and service on Sindri's runtime (code index spec §6, §8, CI-9): 'BuildCodeIndex', the 'code-index-main-move' service, 'EmbedCodeIndex' and the 'code-index-embed' service, the heavy-slot adapter and the map gate's outlooks.</summary>
internal static class CodeIndexJobServiceCollectionExtensions
{
    /// <summary>Adds the job (keyed 'index:&lt;repo&gt;'), the service and the adapters of the code index's ports; the host registers the facade itself.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddCodeIndexJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<CodeIndexJobScopes>()
            .AddScoped<CodeIndexEventSource>()
            .AddScoped<IIndexOutlooks, CodeIndexOutlooks>()
            .AddScoped<Core.Doctor.ICodeIndexChecks, CodeIndexDoctor>()
            .AddScoped<IScheduledService, CodeIndexMainMoveService>()
            .AddSingleton(CodeIndexMainMoveService.BuiltIn)
            .AddScoped<IScheduledService, CodeIndexEmbedService>()
            .AddSingleton(CodeIndexEmbedService.BuiltIn)
            .AddJob<BuildCodeIndexRequest, BuildCodeIndexResult, BuildCodeIndexService>(
                static r => IndexJobs.KeyOf(r.Repo), static r => Summary(r), precheck: null, repo: static r => Core.Ids.RepoName.TryFrom(r.Repo, out var repo) ? repo : null)
            .AddJob<EmbedCodeIndexRequest, EmbedCodeIndexResult, EmbedCodeIndexService>(
                static r => IndexJobs.EmbedKeyOf(r.Repo), static r => EmbedSummary(r), precheck: null, repo: static r => Core.Ids.RepoName.TryFrom(r.Repo, out var repo) ? repo : null);
    }

    /// <summary>The embed job's detail line: what this run made and what it carried, so a full re-embed shows.</summary>
    /// <param name="result">What the job did.</param>
    /// <returns>The line 'sindri jobs show' prints.</returns>
    internal static string EmbedSummary(EmbedCodeIndexResult result) => result.Outcome switch
    {
        EmbedCodeIndexResult.Later => $"{result.Repo}: the index was busy or not built, nothing embedded",
        _ => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{result.Repo}: embedded {result.Made:N0}, carried {result.Carried:N0} of {result.Embeddable:N0}"),
    };

    private static string Summary(BuildCodeIndexResult result) => result.Outcome switch
    {
        "not-on-main" => $"{result.Repo}: left the default branch, nothing indexed",
        _ => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{result.Repo} @ {result.Commit?[..7]}: {result.Files:N0} files, {result.Symbols:N0} symbols{Pending(result)}"),
    };

    private static string Pending(BuildCodeIndexResult result) => result.LayersPending.Count == 0 ? string.Empty : "; " + string.Join(", ", result.LayersPending) + " pending";
}
