using Aw.Judge.Core.Evaluation;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Core;

/// <summary>Registers Aw.Judge.Core's types.</summary>
public static class JudgeCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Judge domain services: the question evaluator (scoped, like the decision log it writes to) and the rules
    /// provider that closes every provider chain.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddJudgeCore(this IServiceCollection services) =>
        services
            .AddScoped<QuestionEvaluator>()
            .AddSingleton<RulesProvider>();
}
