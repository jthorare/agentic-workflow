using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Symbols;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

/// <summary>The map build's input from the record store (spec 2026-10-27 §7.1, §7.5): doc and tracker records as 'record' nodes, cited paths and PR files as 'mentions' edges.</summary>
/// <param name="Records">The store.</param>
/// <param name="Symbols">The code index.</param>
internal sealed record MapRecordSources(IRecordStore Records, ICodeSymbolSource Symbols)
{
    /// <summary>Most resolved paths a pull request keeps as edges; a pull request with more has none.</summary>
    internal const int TouchCap = 50;

    /// <summary>The names of directory docs until 'context.docs.directoryNames' is a profile key (map structure spec §11.2).</summary>
    internal static IReadOnlyList<string> DirectoryNames { get; } = ["AGENTS.md", "README.md", "CONTEXT.md"];

    private const int ListPage = 1000;

    /// <summary>Reads every record in scope.</summary>
    /// <param name="input">What the build has.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The nodes, edges and report lines; the store's error when it can't be read.</returns>
    internal async ValueTask<Result<MapRecordReading>> Read(MapRecordInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var stamps = await Records.Stamps(cancellationToken).ConfigureAwait(false);
        var stored = stamps.IsError ? stamps.Error : await ListAll(cancellationToken).ConfigureAwait(false);
        if (stamps.IsError || stored.IsError)
        {
            return stamps.IsError ? stamps.Error : stored.Error;
        }

        var pass = new RecordPass(Records, Symbols, input, stored.Value);
        return await pass.Run(cancellationToken).ConfigureAwait(false) is { } failed ? failed : pass.Reading(stamps.Value.RecordStamp);
    }

    private async ValueTask<Result<List<StoredRecord>>> ListAll(CancellationToken cancellationToken)
    {
        var all = new List<StoredRecord>();
        RecordKey? after = null;
        while (true)
        {
            var page = await Records.List(new RecordFilter(), after, ListPage, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            if (page.Value.Count == 0)
            {
                return all;
            }

            all.AddRange(page.Value);
            after = page.Value[^1].Key;
        }
    }
}
