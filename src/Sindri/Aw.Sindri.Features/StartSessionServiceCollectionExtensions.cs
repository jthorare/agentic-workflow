using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetLastStart;
using Aw.Sindri.Features.RemoveSessionWorktree;
using Aw.Sindri.Features.StartSession;
using Aw.Sindri.Features.StopSession;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the one-off session slices (agents spec §15.8, A7).</summary>
internal static class StartSessionServiceCollectionExtensions
{
    /// <summary>
    /// Adds StartSession without the command gate (a start takes up to 64 s: the worktree, then tmux; the service takes the gate for its ledger write only) RemoveSessionWorktree and StopSession behind it,
    /// over the act-id map both read.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddStartSession(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<StartPreflight>().AddScoped<StartLaunch>().AddScoped<StartRecord>()
            .AddScoped<IValidator<GetLastStartRequest>, AcceptAllValidator<GetLastStartRequest>>().AddQuery<GetLastStartRequest, LastStartView, GetLastStartService>()
            .AddScoped<IValidator<StartSessionRequest>, AcceptAllValidator<StartSessionRequest>>().AddUngatedCommand<StartSessionRequest, StartedSession, StartSessionService>()
            .AddScoped<IValidator<RemoveSessionWorktreeRequest>, AcceptAllValidator<RemoveSessionWorktreeRequest>>().AddCommand<RemoveSessionWorktreeRequest, RemovedWorktree, RemoveSessionWorktreeService>()
            .AddScoped<IValidator<StopSessionRequest>, AcceptAllValidator<StopSessionRequest>>().AddCommand<StopSessionRequest, StoppedSession, StopSessionService>();
    }
}
