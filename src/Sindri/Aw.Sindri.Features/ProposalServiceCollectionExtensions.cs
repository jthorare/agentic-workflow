using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetProposal;
using Aw.Sindri.Features.ListProposals;
using Aw.Sindri.Features.RejectProposal;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the proposal slices (issue 3787, piece 3): list, show and the owner's reject.</summary>
internal static class ProposalServiceCollectionExtensions
{
    /// <summary>Adds the proposal slices.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddProposals(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<ListProposalsRequest>, AcceptAllValidator<ListProposalsRequest>>().AddQuery<ListProposalsRequest, ListProposalsResponse, ListProposalsService>()
            .AddScoped<IValidator<GetProposalRequest>, AcceptAllValidator<GetProposalRequest>>().AddQuery<GetProposalRequest, GetProposalResponse, GetProposalService>()
            .AddScoped<IValidator<RejectProposalRequest>, AcceptAllValidator<RejectProposalRequest>>().AddCommand<RejectProposalRequest, RejectProposalResponse, RejectProposalService>();
    }
}
