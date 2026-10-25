using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Aw.Context.Core;
using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>
/// The 'onyx' source (spec §5.2): Onyx CE v4.9.0 over its admin search and document routes, live, without as-of cuts.
/// Search sends one 'POST /api/admin/search' (each term quoted as a phrase; a query's UpdatedSince goes as the filter 'time_cutoff'), keeps Onyx's order as the rank (its score
/// is discarded, D3), drops documents an Onyx admin hid (admin search includes them; a standard search doesn't), keys
/// each hit with <see cref="OnyxRecordKeys"/>, marks hits changed after the query's as-of time
/// leaky, and applies the limit itself (Onyx returns up to 50). Fetch reads 'document-size-info', then 'chunk-info' per
/// chunk in order, joined with blank lines; the title and author come from this source's own earlier hit
/// for the document (the fetch routes carry neither), else the id and none, and the updated time from the reference's
/// version. Versions is Unavailable: no v4.9.0 route gives updated_at by id.
/// </summary>
/// <param name="client">The client (key, allowlist, error mapping).</param>
/// <param name="teams">The Linear team keys an identifier alias may name.</param>
/// <param name="clock">The clock: a hit's AsOf is when Onyx answered.</param>
/// <param name="callDeadline">How long Onyx gets to answer each fetch and probe call.</param>
internal sealed class OnyxSource(OnyxClient client, IReadOnlySet<string> teams, TimeProvider clock, TimeSpan callDeadline) : IContextSource, ISourceNotes
{
    private const string SearchRoute = "/api/admin/search";
    private const string SizeRoute = "/api/document/document-size-info";
    private const string ChunkRoute = "/api/document/chunk-info";
    private const string MeRoute = "/api/me";
    private const int MaxRemembered = 1024;
    private static readonly SourceId _id = SourceId.From("onyx");
    private static readonly SourceVersion _unversioned = SourceVersion.From("unversioned");
    private readonly ConcurrentDictionary<string, OnyxHitFacts> _seen = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public SourceDescriptor Describe() => new(_id, SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false);

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var text = string.Join(' ', query.Terms.Select(t => t.Replace('"', ' ').Trim()).Where(t => t.Length > 0).Select(t => $"\"{t}\""));
        if (text.Length == 0 || query.Limit <= 0)
        {
            return Array.Empty<SourceHit>();
        }

        var body = AwJson.Serialize(new OnyxAdminSearchRequest(text, new OnyxSearchFilters(query.UpdatedSince)), OnyxWire.Options);
        var reply = await client.Call<OnyxAdminSearchReply>(OnyxCall.Post(SearchRoute, body), query.Deadline, cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        return Hits(reply.Value.Documents, query);
    }

    /// <inheritdoc />
    public async ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var document = reference.Id.Value;
        var size = await client.Call<OnyxDocumentSizeReply>(OnyxCall.Get(SizeRoute, ("document_id", document)), callDeadline, cancellationToken).ConfigureAwait(false);
        if (size.IsError)
        {
            return size.Error;
        }

        var text = await Text(document, size.Value.NumChunks, cancellationToken).ConfigureAwait(false);
        if (text.IsError)
        {
            return text.Error;
        }

        var facts = _seen.GetValueOrDefault(document);
        return new SourceRecord(reference, OnyxRecordKeys.KindOf(reference.Canonical), facts?.Title ?? document, text.Value,
            facts?.Author, facts?.CreatedAt, facts?.UpdatedAt ?? UpdatedAt(reference.Version));
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<SourceVersion?>>>(ContextErrors.SourceUnavailable.With(
            "Onyx v4.9.0 has no route that gives a document's updated_at by id (spec §5.2), so onyx citations stay 'version unchecked'."));

    /// <inheritdoc />
    public async ValueTask<SourceHealth> Probe(CancellationToken cancellationToken)
    {
        var version = await new OnyxVersionProbe(client.Http).Check(client.Settings, callDeadline, cancellationToken).ConfigureAwait(false);
        if (version.State != SourceState.Ok)
        {
            return version;
        }

        var me = await client.Call<OnyxMeReply>(OnyxCall.Get(MeRoute), callDeadline, cancellationToken).ConfigureAwait(false);
        return me.IsError
            ? new SourceHealth(SourceState.Unavailable, $"{version.Detail}; the key check failed: {me.Error.Message}", me.Error.Code)
            : Role(version.Detail, me.Value);
    }

    // The token's role. Onyx CE has no service-account keys (the route is Business tier), so Sindri's own token is a personal access token of one dedicated admin user, recognised by its email
    // (OnyxSettings.DedicatedAccountEmail, the user `sindri onyx up` makes). A service-account key (Business tier, or a manual setup) is just as dedicated. Any other account, a person's, is a warning,
    // and its address is not echoed.
    private static SourceHealth Role(string versionDetail, OnyxMeReply me)
    {
        var admin = me.EffectivePermissions.Contains("admin", StringComparer.Ordinal);
        var serviceAccount = string.Equals(me.AccountType, "SERVICE_ACCOUNT", StringComparison.Ordinal);
        var dedicatedUser = string.Equals(me.AccountType, "STANDARD", StringComparison.Ordinal)
            && string.Equals(me.Email, OnyxSettings.DedicatedAccountEmail, StringComparison.OrdinalIgnoreCase);
        var account = Shown(me.AccountType);
        return (admin, serviceAccount, dedicatedUser) switch
        {
            (false, _, _) => new SourceHealth(SourceState.Unavailable,
                $"{versionDetail}; key: {account} account without 'admin', which POST /api/admin/search needs (spec §8.8)", ContextErrors.OnyxKeyRefused.Code),
            (true, false, false) => new SourceHealth(SourceState.Degraded,
                $"{versionDetail}; key: admin, {account} account, not Sindri's dedicated user ({OnyxSettings.DedicatedAccountEmail}) or a service account: use a token of a dedicated admin user for Sindri alone "
                + "(`sindri onyx up` makes one; spec §8.8)",
                ContextErrors.OnyxKeyNotServiceAccount.Code),
            (true, true, _) => new SourceHealth(SourceState.Ok, $"{versionDetail}; key: admin, service account", Code: null),
            _ => new SourceHealth(SourceState.Ok, $"{versionDetail}; key: admin, Sindri's dedicated user", Code: null),
        };
    }

    private List<SourceHit> Hits(IReadOnlyList<OnyxSearchDoc> documents, SourceQuery query)
    {
        var now = clock.GetUtcNow();
        var hits = new List<SourceHit>(Math.Min(query.Limit, documents.Count));
        var hidden = 0;
        var unidentified = 0;
        var overLimit = 0;
        foreach (var document in documents)
        {
            if (document.Hidden)
            {
                hidden++;
            }
            else if (hits.Count >= query.Limit)
            {
                overLimit++;
            }
            else if (Hit(document, hits.Count + 1, now, query.AsOf) is { } hit)
            {
                hits.Add(hit);
            }
            else
            {
                unidentified++;
            }
        }

        Note(documents.Count, hits.Count, (hidden, unidentified, overLimit), query.Limit);
        return hits;
    }

    // Every document Onyx found and this source did not return is counted in a note, so the answer says why it has fewer records (or none).
    private void Note(int found, int shown, (int Hidden, int Unidentified, int OverLimit) dropped, int limit)
    {
        if (found == shown)
        {
            return;
        }

        var parts = new List<string>();
        if (dropped.Hidden > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{dropped.Hidden} hidden by an Onyx admin"));
        }

        if (dropped.Unidentified > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{dropped.Unidentified} with no usable id or source identity (check the connector that indexed them)"));
        }

        if (dropped.OverLimit > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{dropped.OverLimit} past the limit of {limit} hits (narrow the question)"));
        }

        _notes.Enqueue(string.Create(CultureInfo.InvariantCulture, $"onyx found {found} document(s) and shows {shown}: {string.Join("; ", parts)}"));
    }

    private readonly ConcurrentQueue<string> _notes = new();

    /// <inheritdoc />
    public IReadOnlyList<string> TakeNotes()
    {
        var taken = new List<string>();
        while (_notes.TryDequeue(out var note))
        {
            taken.Add(note);
        }

        return taken;
    }

    private SourceHit? Hit(OnyxSearchDoc document, int rank, DateTimeOffset now, DateTimeOffset? asOf)
    {
        if (!ExternalId.TryFrom(document.DocumentId, out var id)
            || OnyxRecordKeys.Identify(id, document.SourceType, document.SemanticIdentifier, document.Link, teams) is not { } identity)
        {
            return null;
        }

        Remember(document);
        var version = document.UpdatedAt is { } at ? SourceVersion.From(at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)) : _unversioned;
        var permalink = Uri.TryCreate(document.Link, UriKind.Absolute, out var link) && (link.Scheme == Uri.UriSchemeHttps || link.Scheme == Uri.UriSchemeHttp) ? link : null;
        var leaky = asOf is { } cut && (document.UpdatedAt is null || document.UpdatedAt > cut);
        return new SourceHit(new SourceRef(_id, id, version, permalink, identity.Canonical), document.SemanticIdentifier, rank, now, leaky)
        {
            Aliases = identity.Alias is { } alias ? [alias] : [],
        };
    }

    private void Remember(OnyxSearchDoc document)
    {
        if (_seen.Count >= MaxRemembered)
        {
            _seen.Clear();
        }

        _seen[document.DocumentId] = new OnyxHitFacts(document.SemanticIdentifier, document.PrimaryOwners is [var first, ..] ? first : null, document.UpdatedAt, document.CreatedAt);
    }

    /// <summary>The document's chunks in order, joined with blank lines; every chunk, however long: the record is read whole.</summary>
    private async ValueTask<Result<string>> Text(string document, int chunks, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        for (var chunk = 0; chunk < chunks; chunk++)
        {
            var call = OnyxCall.Get(ChunkRoute, ("document_id", document), ("chunk_id", chunk.ToString(CultureInfo.InvariantCulture)));
            var reply = await client.Call<OnyxChunkReply>(call, callDeadline, cancellationToken).ConfigureAwait(false);
            if (reply.IsError)
            {
                return reply.Error;
            }

            var part = text.Length == 0 ? reply.Value.Content : "\n\n" + reply.Value.Content;
            _ = text.Append(part);
        }

        return text.ToString();
    }

    private static DateTimeOffset? UpdatedAt(SourceVersion version) =>
        DateTimeOffset.TryParse(version.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;

    /// <summary>An account type as a detail shows it: Onyx's upper-case names only, never other text from the reply.</summary>
    private static string Shown(string accountType) =>
        accountType.Length is > 0 and <= 32 && accountType.All(c => char.IsAsciiLetterUpper(c) || c == '_') ? accountType : "an unrecognized";
}
