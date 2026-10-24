using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Traits;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Infrastructure;

/// <summary>Registers the adapters trait-check reads and writes through.</summary>
public static class TraitCheckServiceCollectionExtensions
{
    /// <summary>
    /// Adds the trait files ('&lt;repo&gt;/.agents/traits' and the overlay '$AW_STATE_DIR/traits/&lt;repo-name&gt;', else
    /// '~/.agentic-workflow/traits'), the repo's text files read from the process's working directory, and the trait-check
    /// log ('trait-check/log.jsonl' in the judge's state directory). Called by AddJudgeInfrastructure.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddTraitCheckInfrastructure(this IServiceCollection services, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var overlay = Path.Combine(
            environment("AW_STATE_DIR") is { Length: > 0 } state ? state : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow"),
            "traits");
        return services
            .AddSingleton<ITraitStore>(new FileTraitStore(overlay))
            .AddSingleton<ITraitFiles>(_ => new FileTraitFiles(Environment.CurrentDirectory))
            .AddScoped<ITraitJudge, JevTraitJudge>()
            .AddSingleton<ITraitCheckLog>(sp => new FileTraitCheckLog(Path.Combine(sp.GetRequiredService<JudgeSettings>().StateDirectory, "trait-check"), sp.GetRequiredService<TimeProvider>()));
    }
}
