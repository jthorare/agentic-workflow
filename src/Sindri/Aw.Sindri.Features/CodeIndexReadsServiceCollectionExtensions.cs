using Aw.CodeIndex.Core.Facade;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Features.CodeIndexReads;
using Aw.Sindri.Features.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the code index's read slices (code-index spec §10, CI-10a): status, symbols, callers and callees.</summary>
internal static class CodeIndexReadsServiceCollectionExtensions
{
    /// <summary>Adds the four reads as queries over <see cref="ICodeIndex"/>, which the host registers.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddCodeIndexReads(this IServiceCollection services)
    {
        _ = services.AddScoped<CodeIndexScopes>().AddScoped<RepoHealths>().AddScoped<ICodeIndexHealth, CodeIndexHealth>();
        _ = services.AddScoped<IValidator<GetCodeIndexStatusRequest>, AcceptAllValidator<GetCodeIndexStatusRequest>>().AddQuery<GetCodeIndexStatusRequest, CodeIndexStatusView, GetCodeIndexStatusService>();
        _ = services.AddScoped<IValidator<FindCodeSymbolsRequest>, AcceptAllValidator<FindCodeSymbolsRequest>>().AddQuery<FindCodeSymbolsRequest, FindSymbolsAnswer, FindCodeSymbolsService>();
        _ = services.AddScoped<IValidator<GetCodeCallersRequest>, AcceptAllValidator<GetCodeCallersRequest>>().AddQuery<GetCodeCallersRequest, CodeCallsView, GetCodeCallersService>();
        _ = services.AddScoped<IValidator<GetCodeCalleesRequest>, AcceptAllValidator<GetCodeCalleesRequest>>().AddQuery<GetCodeCalleesRequest, CodeCallsView, GetCodeCalleesService>();
        return services;
    }
}
