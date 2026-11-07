using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.LinearIssues;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the Linear hub's comment and create writes (Linear hub spec §3, §5.2, §5.3; #2077 Ph3 1b).</summary>
internal static class LinearCommentCreateServiceCollectionExtensions
{
    /// <summary>Adds CommentLinearIssue and CreateLinearIssue behind the command chain.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddLinearCommentCreate(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<CommentLinearIssueRequest>, AcceptAllValidator<CommentLinearIssueRequest>>().AddCommand<CommentLinearIssueRequest, CommentLinearIssueResponse, CommentLinearIssueService>()
            .AddScoped<IValidator<CreateLinearIssueRequest>, AcceptAllValidator<CreateLinearIssueRequest>>().AddCommand<CreateLinearIssueRequest, CreateLinearIssueResponse, CreateLinearIssueService>();
    }
}
