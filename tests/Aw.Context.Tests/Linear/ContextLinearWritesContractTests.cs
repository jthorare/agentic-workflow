using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Linear;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.Linear;

/// <summary>
/// The Linear writes contract over the real facade, built through the container on the stateful fake Linear: the live reads, the write client, the write-through into a real store. It passes once the container also holds the write client (the lead's registration of
/// <see cref="ILinearWrites"/> at merge); before that the container hands out the unwired writes and every row fails on the first send. Client ids are read from the variables of the create mutations (the variables' input.id).
/// </summary>
[Trait("Lane", "integration")]
public sealed class ContextLinearWritesContractTests : ContextLinearWritesContract<IContextLinearWrites>, IAsyncLifetime
{
    private LinearHubWorld _world = null!;

    protected override JsonElement? HumanSettings => _world.Scope.Settings;

    protected override ExternalId TeamId => ExternalId.From(_world.Linear.TeamId("ENG"));

    protected override LinearIssueKey IssueKey => LinearIssueKey.Parse("ENG-1").Value;

    protected override ExternalId CommentId => ExternalId.From(_world.Linear.Comments[0].Id);

    public async ValueTask InitializeAsync() =>
        _world = await LinearHubWorld.Start(seed => seed
            .Team("ENG")
            .Member("Ada Fixture")
            .Issue("ENG", "Fix the login timeout", new FakeLinearIssueSeed { State = "In Progress", Assignee = "Ada Fixture" })
            .Comment("ENG-1", "first comment"));

    public ValueTask DisposeAsync() => _world.DisposeAsync();

    protected override IContextLinearWrites Create() => _world.Provider.GetRequiredService<IContextLinearWrites>();

    protected override int Sent(IContextLinearWrites writes) => _world.Linear.RequestBodies.Count;

    protected override int WritesSent(IContextLinearWrites writes) => _world.Linear.RequestBodies.Count(static b => ((string?)(JsonNode.Parse(b) as JsonObject)?["query"])?.TrimStart().StartsWith("mutation", StringComparison.Ordinal) == true);

    protected override IReadOnlyList<LinearClientId> ClientIdsSent(IContextLinearWrites writes) =>
        [.. _world.Linear.RequestBodies.Select(Creating).OfType<JsonObject>().Select(static m => (string?)m["variables"]?["input"]?["id"]).OfType<string>().Select(static id => LinearClientId.Parse(id).Value)];

    protected override void MakeNextWriteUnknown(IContextLinearWrites writes) => _world.Linear.Switches.DropBeforeApplying = true;

    // the request when it is a create mutation, else null
    private static JsonObject? Creating(string body)
    {
        var request = JsonNode.Parse(body) as JsonObject;
        var query = (string?)request?["query"];
        return query is not null && (query.Contains("issueCreate", StringComparison.Ordinal) || query.Contains("commentCreate", StringComparison.Ordinal)) ? request : null;
    }
}
