using System.Reflection;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Vectors;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An <see cref="IContextEngine"/> that answers only the embed run and the progress read (one source holding a set number of records) and refuses every other call.</summary>
public class EmbedEngineProxy : DispatchProxy
{
    /// <summary>What the embed run answers.</summary>
    public Result<EmbedRecordsReport> Report { get; set; } = new EmbedRecordsReport(0, 0, Done: true);

    /// <summary>How many records the one source holds, as the progress read counts them.</summary>
    public int? Records { get; set; }

    /// <summary>The engine view of this proxy.</summary>
    internal IContextEngine Proxy => (IContextEngine)(object)this;

    internal static EmbedEngineProxy Create() => (EmbedEngineProxy)(object)Create<IContextEngine, EmbedEngineProxy>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        switch (targetMethod?.Name)
        {
            case nameof(IContextEngine.Embed):
                return new ValueTask<Result<EmbedRecordsReport>>(Report);
            case nameof(IContextEngine.Progress):
                Result<IReadOnlyList<SourceProgress>> rows = new List<SourceProgress> { new(SourceId.From("linear"), Phase: null, LastSync: null, LastCode: null, Usable: true, Complete: true, Records: Records) };
                return new ValueTask<Result<IReadOnlyList<SourceProgress>>>(rows);
            default:
                throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
