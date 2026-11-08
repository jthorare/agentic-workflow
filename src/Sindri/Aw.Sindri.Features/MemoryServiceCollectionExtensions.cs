using Aw.Kernel.Services;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.CreateMemory;
using Aw.Sindri.Features.ExpireMemory;
using Aw.Sindri.Features.ForgetMemoryFact;
using Aw.Sindri.Features.ListMemories;
using Aw.Sindri.Features.SearchMemory;
using Aw.Sindri.Features.WriteMemoryFact;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the memory slices (agent-first PR 13).</summary>
internal static class MemoryServiceCollectionExtensions
{
    /// <summary>Adds CreateMemory, WriteMemoryFact and ForgetMemoryFact as plain services (agent sessions may use them), ListMemories and SearchMemory behind the query chain, and the 'memory-expire' built-in service.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddMemorySlices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IService<CreateMemoryRequest, CreateMemoryResponse>, CreateMemoryService>()
            .AddScoped<IService<WriteMemoryFactRequest, WriteMemoryFactResponse>, WriteMemoryFactService>()
            .AddScoped<IService<ForgetMemoryFactRequest, ForgetMemoryFactResponse>, ForgetMemoryFactService>()
            .AddScoped<IValidator<ListMemoriesRequest>, AcceptAllValidator<ListMemoriesRequest>>().AddQuery<ListMemoriesRequest, ListMemoriesResponse, ListMemoriesService>()
            .AddScoped<IValidator<SearchMemoryRequest>, AcceptAllValidator<SearchMemoryRequest>>().AddQuery<SearchMemoryRequest, SearchMemoryResponse, SearchMemoryService>()
            .AddScoped<IScheduledService, ExpireMemoryService>()
            .AddSingleton(ExpireMemoryService.BuiltIn);   // runs without a profile entry, so PauseService refuses it
    }
}
