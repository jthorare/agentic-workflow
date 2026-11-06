using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Testing.Fakes;

namespace Aw.Context.Testing.Linear;

/// <summary>
/// A stateful fake Linear on loopback (spec D15): 'POST /graphql' serves the queries the linear feed sends and the write
/// mutations, answered from state, so a write changes what the next read returns. <see cref="Switches"/> makes it misbehave.
/// The Authorization header must equal <see cref="Token"/> (or 'Bearer ' and it); no header is recorded or logged. See the README beside this file.
/// </summary>
public sealed class FakeLinear : IAsyncDisposable
{
    /// <summary>The credential a fake started without a token accepts (a fixture value, not a secret).</summary>
    public const string DefaultToken = "fixture-linear";

    /// <summary>The request limit the rate-limit headers report.</summary>
    public const int RequestLimit = 5000;

    private const string Graphql = "/graphql";
    private readonly Lock _gate = new();
    private readonly LinearStore _store;
    private readonly LinearQueries _queries;
    private readonly LinearMutations _mutations;
    private readonly List<string> _bodies = [];
    private LoopbackServer _server = null!;
    private readonly string? _writeToken;
    private int _requests;

    private FakeLinear(LinearStore store, string token, string? writeToken)
    {
        _store = store;
        Token = token;
        _writeToken = writeToken;
        _queries = new LinearQueries(store);
        _mutations = new LinearMutations(store, Switches);
    }

    /// <summary>The GraphQL endpoint, 'http://127.0.0.1:&lt;port&gt;/graphql'.</summary>
    public Uri Url => new(_server.BaseUrl, "graphql");

    /// <summary>The credential the fake accepts.</summary>
    public string Token { get; }

    /// <summary>The misbehaviour switches; set them at any time.</summary>
    public FakeLinearSwitches Switches { get; } = new();

    /// <summary>The issues now, archived ones included.</summary>
    public IReadOnlyList<FakeLinearIssue> Issues => Locked<IReadOnlyList<FakeLinearIssue>>(() => [.. _store.Issues]);

    /// <summary>The comments now.</summary>
    public IReadOnlyList<FakeLinearComment> Comments => Locked<IReadOnlyList<FakeLinearComment>>(() => [.. _store.Comments]);

    /// <summary>The attachments now.</summary>
    public IReadOnlyList<FakeLinearAttachment> Attachments => Locked<IReadOnlyList<FakeLinearAttachment>>(() => [.. _store.Attachments]);

    /// <summary>The request bodies received, in order. Headers, and so the token, are never recorded.</summary>
    public IReadOnlyList<string> RequestBodies => Locked<IReadOnlyList<string>>(() => [.. _bodies]);

    /// <summary>Starts the fake on an ephemeral loopback port.</summary>
    /// <param name="seed">Fills the made-up data; null seeds one team, 'ENG'.</param>
    /// <param name="token">The credential to accept; null takes <see cref="DefaultToken"/>.</param>
    /// <param name="writeToken">A second credential to accept, for a client that keeps a read key and a write key apart (spec D2); null accepts only <paramref name="token"/>. The fake treats both alike.</param>
    /// <returns>The running fake.</returns>
    public static async Task<FakeLinear> Start(Action<FakeLinearSeed>? seed = null, string? token = null, string? writeToken = null)
    {
        var data = new FakeLinearSeed();
        (seed ?? (s => s.Team("ENG")))(data);
        var linear = new FakeLinear(data.Store, token ?? DefaultToken, writeToken);
        linear._server = await LoopbackServer.Start("fake-linear-stateful", linear.Handle).ConfigureAwait(false);
        return linear;
    }

    /// <summary>The id of a seeded team.</summary>
    /// <param name="key">The team key.</param>
    /// <returns>Its id.</returns>
    public string TeamId(string key) => Id(r => r.Teams, t => t.Key == key, key);

    /// <summary>The id of a seeded workflow state.</summary>
    /// <param name="teamKey">The team key.</param>
    /// <param name="name">The state name.</param>
    /// <returns>Its id.</returns>
    public string StateId(string teamKey, string name)
    {
        var team = TeamId(teamKey);
        return Id(r => r.States, s => s.TeamId == team && s.Name == name, name);
    }

    /// <summary>The id of a member.</summary>
    /// <param name="name">The name.</param>
    /// <returns>Its id.</returns>
    public string MemberId(string name) => Id(r => r.Members, m => m.Name == name, name);

    /// <summary>The id of a label.</summary>
    /// <param name="name">The name.</param>
    /// <returns>Its id.</returns>
    public string LabelId(string name) => Id(r => r.Labels, l => l.Name == name, name);

    /// <summary>The id of a cycle.</summary>
    /// <param name="name">The name.</param>
    /// <returns>Its id.</returns>
    public string CycleId(string name) => Id(r => r.Cycles, c => c.Name == name, name);

    /// <summary>The id of a project.</summary>
    /// <param name="name">The name.</param>
    /// <returns>Its id.</returns>
    public string ProjectId(string name) => Id(r => r.Projects, p => p.Name == name, name);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _server.DisposeAsync();

    private static FakeHttpReply Error(int status, string code, string message, string operation) =>
        new(status, new JsonObject { ["errors"] = new JsonArray(new JsonObject { ["message"] = message, ["extensions"] = new JsonObject { ["code"] = code } }) }, operation);

    private string Id(Func<LinearStore, List<LinearRow>> rows, Predicate<LinearRow> match, string what) =>
        Locked(() => rows(_store).Find(match)?.Id ?? throw new InvalidOperationException($"The fake Linear has no seeded '{what}'."));

    private T Locked<T>(Func<T> read)
    {
        lock (_gate)
        {
            return read();
        }
    }

    private FakeHttpReply Handle(FakeHttpRequest request)
    {
        lock (_gate)
        {
            _requests++;
            _bodies.Add(request.Body);
            var reply = Route(request);
            return reply with { Headers = RateHeaders() };
        }
    }

    private Dictionary<string, string> RateHeaders()
    {
        var limited = Switches.RateLimit != LinearRateLimit.None;
        var remaining = limited ? 0 : Math.Max(0, RequestLimit - _requests);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["x-ratelimit-requests-limit"] = RequestLimit.ToString(CultureInfo.InvariantCulture),
            ["x-ratelimit-requests-remaining"] = remaining.ToString(CultureInfo.InvariantCulture),
            ["x-ratelimit-requests-reset"] = "1767229200000",
            ["x-complexity"] = "1",
        };
    }

    private FakeHttpReply Route(FakeHttpRequest request)
    {
        if (Switches.RevokeKey || !TokenOk(request.Authorization))
        {
            return Error(401, "AUTHENTICATION_ERROR", "Authentication required, not authenticated.", "refused: no valid credential");
        }

        if (request.Method != "POST" || request.Path != Graphql)
        {
            return Error(404, "NOT_FOUND", $"The fake Linear answers POST {Graphql} only, not {request.Method} {request.Path}.", "refused: route");
        }

        if (Switches.RateLimit != LinearRateLimit.None)
        {
            return Error(Switches.RateLimit == LinearRateLimit.Http429 ? 429 : 400, "RATELIMITED", "Rate limit exceeded.", "refused: rate limited");
        }

        try
        {
            return Answer(request.Body);
        }
        catch (FakeRequestException e)
        {
            return Error(e.Status, e.Code, e.Message, "refused: " + e.Code);
        }
    }

    private bool TokenOk(string? authorization) => Accepts(authorization, Token) || (_writeToken is not null && Accepts(authorization, _writeToken));

    private static bool Accepts(string? authorization, string token) =>
        string.Equals(authorization, token, StringComparison.Ordinal) || string.Equals(authorization, "Bearer " + token, StringComparison.Ordinal);

    private FakeHttpReply Answer(string body)
    {
        var document = Parse(body);
        var query = document["query"] is JsonValue q && q.GetValueKind() == JsonValueKind.String
            ? q.GetValue<string>()
            : throw FakeRequestException.Invalid("The request has no string 'query'.");
        var (mutation, fields) = GraphQlReader.Read(query, document["variables"] as JsonObject, allowMutation: true, allowObjects: true);
        var operation = string.Join(' ', fields.Select(f => f.Name));
        if (!mutation)
        {
            ApplyTeammate();
        }

        var data = new JsonObject();
        var errors = new JsonArray();
        foreach (var field in fields)
        {
            if (mutation)
            {
                Mutate(field, data, errors);
                continue;
            }

            var (value, error) = _queries.Resolve(field);
            data[field.ResponseKey] = value;
            if (error is not null)
            {
                errors.Add(new JsonObject { ["message"] = error, ["path"] = new JsonArray(field.ResponseKey), ["extensions"] = new JsonObject { ["code"] = "INVALID_INPUT" } });
            }
        }

        return Finish(mutation, operation, data, errors);
    }

    private FakeHttpReply Finish(bool mutation, string operation, JsonObject data, JsonArray errors)
    {
        if (mutation && Switches.NoAnswer)
        {
            Switches.NoAnswer = false;
            return FakeHttpReply.Dropped("dropped after applying: " + operation);
        }

        var reply = new JsonObject { ["data"] = data };
        if (errors.Count > 0)
        {
            reply["errors"] = errors;
        }

        return new FakeHttpReply(200, reply, operation);
    }

    private void Mutate(GraphQlField field, JsonObject data, JsonArray errors)
    {
        try
        {
            data[field.ResponseKey] = LinearShape.Select(_mutations.Run(field), field, field.Name);
        }
        catch (LinearMutationException e)
        {
            var error = (JsonObject)e.Error.DeepClone();
            error["path"] = new JsonArray(field.ResponseKey);
            data[field.ResponseKey] = null;
            errors.Add(error);
        }
    }

    // why: a teammate's edit or a move lands between the client's write and its next read
    private void ApplyTeammate()
    {
        if (Switches.AnswerWithNewerField)
        {
            Switches.AnswerWithNewerField = false;
            _ = _mutations.TeammateEditsLastWritten();
        }

        if (Switches.MoveTeam is { } team)
        {
            Switches.MoveTeam = null;
            _ = _mutations.MoveLastWritten(team);
        }
    }

    private static JsonObject Parse(string body)
    {
        try
        {
            return JsonNode.Parse(body) as JsonObject ?? throw FakeRequestException.Invalid("The body isn't a JSON object.");
        }
        catch (JsonException e)
        {
            throw FakeRequestException.Invalid("The body isn't JSON: " + e.Message);
        }
    }
}
