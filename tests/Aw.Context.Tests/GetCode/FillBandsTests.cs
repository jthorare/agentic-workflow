using System.Diagnostics;
using Aw.Context.Core.Engine;

namespace Aw.Context.Tests.GetCode;

/// <summary>Each fill band has a span under the 'Aw.Context' source, so the exporter shows where a concurrent fill spends its time.</summary>
public sealed class FillBandsTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_listened_band_yields_a_span_named_for_it()
    {
        var started = new List<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Aw.Context",
            Sample = (ref _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => started.Add(activity.OperationName),
        };
        ActivitySource.AddActivityListener(listener);

        using (FillBands.Start(FillBands.CodeLexical))
        {
        }

        using (FillBands.Start(FillBands.History))
        {
        }

        Assert.Contains(FillBands.CodeLexical, started, StringComparer.Ordinal);
        Assert.Contains(FillBands.History, started, StringComparer.Ordinal);
    }
}
