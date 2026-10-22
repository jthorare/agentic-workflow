using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>Stubs and builders the context Step input tests share.</summary>
internal static class ContextInputFixtures
{
    internal static readonly RepoName Web = RepoName.From("web");

    internal static LoadedProfile Profile() =>
        new(new SindriProfile(1, "thor", Repos: [new RepoEntry("web", "/work/web"), new RepoEntry("api", "/work/api")]), ProfileHash.From(new string('a', 64)), "/home/profile.yaml");

    internal static LensesSetView SmallSet(params string[] anchors)
    {
        var nodes = anchors.Select(a => new LensAnchor(NodeKey.From(a), a, NodeKind.Feature, Trust.Trusted)).ToArray();
        return new LensesSetView(2, nodes, [], "v3", Draft: false, 2, DateTimeOffset.UnixEpoch,
            new LensBands(
                new IntentBand([], [], new IntentCounts(0, 0, Orphaned: null), Truncated: false),
                new ProductBand([.. nodes.Select(n => new LensItem(n.Key, n.Label, NodeKind.Feature, Provenance.Human, 1, Confirmed: true, Trust.Trusted))], [], [], new ProductCounts(nodes.Length, 0), [], Truncated: false),
                new CodeBand([], LensSymbols.Unavailable, new CodeCounts(0, 0, 0), Truncated: false)),
            [], [],
            new LensCoverage(nodes.Length, new CoverageIntent(0, nodes.Length), new CoverageProduct(nodes.Length, nodes.Length), new CoverageCode(0, nodes.Length, "file", 0), []),
            new LensSearched([], "v3", 2, [], [], new SearchedBands(new BandSearched(Empty: true), new BandSearched(Empty: false), new BandSearched(Empty: true))),
            [], new Dictionary<string, Citation>(StringComparer.Ordinal));
    }

    /// <summary>Stored fact contents by sha, for the fact-reference arguments; a sha it doesn't hold can't be read.</summary>
    internal sealed class MemoryFacts : IFactContents
    {
        private readonly Dictionary<string, byte[]> _stored = new(StringComparer.Ordinal);

        /// <summary>Stores <paramref name="content"/> and returns its reference.</summary>
        internal FactValue Put(string content)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
            _stored[sha] = bytes;
            return new FactValue("fact:sha256:" + sha, sha, HeadSha: null);
        }

        /// <summary>Overwrites what <paramref name="fact"/> points at, as if the stored content changed after it was recorded.</summary>
        internal void Change(FactValue fact, string content) => _stored[fact.Sha256] = System.Text.Encoding.UTF8.GetBytes(content);

        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(_stored.TryGetValue(fact.Sha256, out var bytes)
                ? (Result<ReadOnlyMemory<byte>>)(ReadOnlyMemory<byte>)bytes
                : Core.SindriErrors.ApiInternal.With("not stored"));
    }

    internal static ContextInputFacts NoFacts() => new(new MemoryFacts());

    internal sealed class Profiles(LoadedProfile? profile) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult<Result<LoadedProfile?>>(profile);
    }
}
