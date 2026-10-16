using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflows.Effects;

/// <summary>Reads an effect's ledger rows (engine spec §5.6, §5.7) the way the dispatch tests check them.</summary>
internal static class EffectRows
{
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(20);

    /// <summary>Every ledger event of <paramref name="type"/>, oldest first.</summary>
    public static async Task<IReadOnlyList<LedgerEvent>> Events(IServiceProvider sindri, string type)
    {
        var read = await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, TestContext.Current.CancellationToken);
        Assert.False(read.IsError, read.IsError ? read.Error.Message : null);
        return [.. read.Value.Where(e => e.Envelope.Type == type)];
    }

    /// <summary>The <see cref="EffectRow"/> payloads of every row of <paramref name="type"/> from <paramref name="origin"/>.</summary>
    public static async Task<IReadOnlyList<EffectRow>> Of(IServiceProvider sindri, string type, CommandOrigin origin)
    {
        var events = sindri.GetRequiredService<EventFactory>();
        return [.. (await Events(sindri, type)).Select(e => Payload<EffectRow>(events, e)).Where(r => r.Origin == origin)];
    }

    /// <summary>The rows of <paramref name="type"/> from <paramref name="origin"/>, read until there is one (the rows follow the workflow task).</summary>
    public static async Task<IReadOnlyList<EffectRow>> Eventually(IServiceProvider sindri, string type, CommandOrigin origin)
    {
        var deadline = DateTime.UtcNow + _patience;
        while (DateTime.UtcNow < deadline)
        {
            if (await Of(sindri, type, origin) is { Count: > 0 } rows)
            {
                return rows;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.Fail($"no {origin} {type} row was written");
        return [];
    }

    /// <summary>A recorded event's payload.</summary>
    public static T Payload<T>(EventFactory events, LedgerEvent recorded)
    {
        var read = events.Payload<T>(recorded);
        Assert.False(read.IsError, read.IsError ? read.Error.Message : null);
        return read.Value;
    }
}
