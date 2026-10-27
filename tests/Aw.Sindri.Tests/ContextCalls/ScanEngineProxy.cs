using System.Reflection;
using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An <see cref="IContextEngine"/> that answers only the map's publish state and the concept scan, counts the scans and refuses every other call.</summary>
public class ScanEngineProxy : DispatchProxy
{
    /// <summary>What the publish-state call answers: no hold, no draft, no current version.</summary>
    public MapPublishState State { get; set; } = new(Hold: null, LatestDraft: null, Current: null, CurrentHeads: []);

    /// <summary>What the scan answers.</summary>
    public Result<ConceptScanReport> Report { get; set; } = new ConceptScanReport(Version: 7, OnDraft: true, ConceptScanOutcome.Scanned, Hints: 2, Searches: 3);

    /// <summary>How many scans were asked for.</summary>
    public int Scans { get; private set; }

    /// <summary>The engine view of this proxy.</summary>
    internal IContextEngine Proxy => (IContextEngine)(object)this;

    internal static ScanEngineProxy Create() => (ScanEngineProxy)(object)Create<IContextEngine, ScanEngineProxy>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        switch (targetMethod?.Name)
        {
            case nameof(IContextEngine.Held):
                return new ValueTask<Result<MapPublishState>>(State);
            case nameof(IContextEngine.ScanConcepts):
                Scans++;
                return new ValueTask<Result<ConceptScanReport>>(Report);
            default:
                throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
