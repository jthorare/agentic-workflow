using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflows.Effects;

/// <summary>
/// A stand-in for 'effect.notify.desk' (engine P6 unit (c) builds the real one): it performs each effect identity once,
/// as the real block's existence check does, records every call, and replies 'delivered' to a recipe effect.
/// </summary>
internal sealed class FakeDeskEffect : IEffectKind
{
    private readonly Lock _gate = new();
    private readonly List<EffectContext> _calls = [];
    private readonly HashSet<string> _performed = new(StringComparer.Ordinal);

    /// <summary>When set, every recipe effect's dispatch fails with it (a setup the effect can't run in) instead of performing the effect; the engine's own notices still go through.</summary>
    public Error? Failure { get; set; }

    /// <summary>When set, every recipe effect's dispatch throws it (a block that fails outside the Result path); the engine's own notices still go through.</summary>
    public Exception? Throw { get; set; }

    /// <summary>The outcome a recipe effect replies; 'delivered' is the one registered reply event declares.</summary>
    public string ReplyOutcome { get; set; } = "delivered";

    /// <summary>Every dispatch, in order.</summary>
    public IReadOnlyList<EffectContext> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>The recipe effect's dispatches only: the engine's own notices (unit (c)) go through this block too.</summary>
    public IReadOnlyList<EffectContext> RecipeCalls => [.. Calls.Where(static c => !c.FromEngine)];

    /// <summary>The recipe effect's identities performed, once each.</summary>
    public IReadOnlyList<string> RecipePerformed => [.. RecipeCalls.Select(static c => c.Identity).Distinct(StringComparer.Ordinal)];

    /// <summary>The identities performed, once each.</summary>
    public IReadOnlyList<string> Performed
    {
        get
        {
            lock (_gate)
            {
                return [.. _performed];
            }
        }
    }

    /// <summary>Registers this fake as the runtime of 'effect.notify.desk', replacing the shipped one.</summary>
    public void Replace(IServiceCollection services) =>
        services.AddKeyedScoped<IEffectKind>("effect.notify.desk", (_, _) => this);

    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        bool existed;
        lock (_gate)
        {
            _calls.Add(context);
            if (Throw is { } thrown && !context.FromEngine)
            {
                throw thrown;
            }

            if (Failure is { } failure && !context.FromEngine)
            {
                return ValueTask.FromResult<Result<EffectDone>>(failure);
            }

            existed = !_performed.Add(context.Identity);
        }

        return ValueTask.FromResult<Result<EffectDone>>(new EffectDone(context.FromEngine ? null : OutcomeName.From(ReplyOutcome), existed));
    }
}
